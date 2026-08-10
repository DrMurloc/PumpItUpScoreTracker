using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Restart recovery for the score pipeline (docs/design/import-restart-recovery.md).
///     <para>
///         A batch lives in memory for two minutes before it announces itself, so a restart in
///         that window keeps every score and loses everything derived from them — highlights,
///         folder lamps, ratings, titles, the personalized tier list, the session card — while the
///         import reports success. This rebuilds the lost announcement from the journal.
///     </para>
///     <para>
///         The marker is stamped here rather than sent from PlayerProgress: that vertical cannot
///         reference this one (ScoreLedger → Communities → PlayerProgress, so the reference would
///         close a cycle), and consuming its published event points the dependency the way it
///         already runs.
///     </para>
/// </summary>
internal sealed class SessionRecoverySaga :
    IRequestHandler<ReplaySessionCommand, int>,
    IRequestHandler<GetUnprocessedSessionsQuery, IReadOnlyList<ScoreSessionRecord>>,
    IConsumer<ScoreHighlightsCapturedEvent>
{
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IScoreJournalRepository _journal;
    private readonly ILogger<SessionRecoverySaga> _logger;
    private readonly IPhoenixRecordRepository _records;
    private readonly IScoreSessionRepository _sessions;
    private readonly IBus _bus;

    public SessionRecoverySaga(IScoreSessionRepository sessions, IScoreJournalRepository journal,
        IPhoenixRecordRepository records, IBus bus, IDateTimeOffsetAccessor dateTime,
        ILogger<SessionRecoverySaga> logger)
    {
        _sessions = sessions;
        _journal = journal;
        _records = records;
        _bus = bus;
        _dateTime = dateTime;
        _logger = logger;
    }

    public Task<IReadOnlyList<ScoreSessionRecord>> Handle(GetUnprocessedSessionsQuery request,
        CancellationToken cancellationToken)
    {
        return _sessions.ListUnprocessed(cancellationToken);
    }

    public async Task<int> Handle(ReplaySessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessions.Get(request.SessionId, cancellationToken);
        if (session is null) return 0;
        // Re-checked here rather than trusted from the caller: the pass that sends this reads its
        // candidates from one vertical and gates them in another, and a live drain can land in
        // between.
        if (session.ProcessedAt is not null) return 0;

        var entries = await _journal.GetSessionEntries(request.UserId, request.SessionId, cancellationToken);
        var chartIds = entries.Select(e => e.ChartId).Distinct().ToArray();
        if (chartIds.Length == 0)
        {
            // A session that wrote nothing has nothing to announce, and leaving it unprocessed
            // would make it a candidate on every boot from here on.
            await _sessions.MarkProcessed(request.SessionId, _dateTime.Now, cancellationToken);
            return 0;
        }

        var histories = await _journal.GetChartHistories(request.UserId, chartIds, cancellationToken);
        var replayed = SessionReplayBuilder.Build(session.Mix, entries, histories);
        if (replayed.Count == 0)
        {
            await _sessions.MarkProcessed(request.SessionId, _dateTime.Now, cancellationToken);
            return 0;
        }

        var batch = new PendingScoreBatch(session.Mix,
            replayed.Where(c => c.IsNewPass).Select(c => c.ChartId).ToArray(),
            replayed.Where(c => !c.IsNewPass).ToDictionary(c => c.ChartId, c => c.OldScore!.Value),
            request.SessionId);

        var involved = replayed.Select(c => c.ChartId).ToHashSet();
        var bests = (await _records.GetRecordedScores(session.Mix, request.UserId, cancellationToken) ?? [])
            .Where(r => involved.Contains(r.ChartId))
            .ToDictionary(r => r.ChartId);

        // SET, not add: Touch already ran for any batch that drained before the interruption, and
        // the counts here are the whole session's totals recomputed from the journal.
        await _sessions.SetCounts(request.SessionId, _dateTime.Now, batch.NewChartIds.Length,
            batch.UpscoredChartIds.Count, cancellationToken);

        // Ask before boxing five arguments for a line nobody may be listening to (CA1873).
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "Replaying session {SessionId} for {UserId} on {Mix}: {New} new, {Upscored} upscored",
                request.SessionId, request.UserId, session.Mix, batch.NewChartIds.Length,
                batch.UpscoredChartIds.Count);

        // The marker is NOT stamped here — the capture chain stamps it when it finishes, the same
        // way a live batch does. Stamping on publish would mark a session processed whose work is
        // still in flight, and lose it for good if this process dies again.
        await _bus.Publish(PlayerScoresUpdatedEvent.Create(_dateTime.Now, request.UserId, session.Mix,
            ScoreChangeAssembler.Build(batch, bests), request.SessionId), cancellationToken);

        return replayed.Count;
    }

    /// <summary>
    ///     The capture chain reached the end for this session, so it never needs replaying.
    ///     ScoreHighlightsCapturedEvent is the right signal because HighlightCaptureSaga publishes
    ///     it unconditionally — every inner step is failure-isolated, so a session with nothing
    ///     noteworthy in it still gets stamped.
    /// </summary>
    public async Task Consume(ConsumeContext<ScoreHighlightsCapturedEvent> context)
    {
        if (context.Message.SessionId is not { } sessionId) return;
        await _sessions.MarkProcessed(sessionId, _dateTime.Now, context.CancellationToken);
    }
}
