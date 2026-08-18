using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Journals plays that never became a record, so a chart's history holds the attempts and
///     not only the improvements — the recent window's losing runs, and the stage breaks from
///     both surfaces. Writes nothing to the ledger's record — that is
///     <see cref="UpdatePhoenixRecordHandler" />'s job, and a play arriving through both paths
///     collapses onto one row by its play key.
/// </summary>
internal sealed class RecordObservedPlaysHandler(IScoreJournalRepository journal, IMemoryCache cache,
        IChartRepository charts, ILogger<RecordObservedPlaysHandler> logger)
    : IRequestHandler<RecordObservedPlaysCommand>
{
    public async Task Handle(RecordObservedPlaysCommand request, CancellationToken cancellationToken)
    {
        // A walk-off is never stored, whether or not it would have been a best.
        var plays = request.Plays
            .Where(p => !BestAttemptPolicy.IsWalkOff(p.IsBroken, p.Score, p.Judgements))
            .ToArray();
        if (plays.Length == 0) return;

        // One catalog read per chart, for the combo and the tripwire. Charts appear many times
        // in a window (the same song replayed all evening) so this is well under one per row.
        var noteCounts = new Dictionary<Guid, int?>();
        foreach (var chartId in plays.Where(p => p.Judgements != null).Select(p => p.ChartId).Distinct())
            noteCounts[chartId] = await NoteCountWatch.NoteCountFor(charts, request.Mix, chartId, cancellationToken);

        // One line per chart, not per play: a window holds several runs of the same song, and a
        // drifted catalog would otherwise repeat itself inside a single import.
        var warned = new HashSet<Guid>();
        var entries = plays.Select(p =>
            {
                var noteCount = noteCounts.GetValueOrDefault(p.ChartId);
                if (warned.Add(p.ChartId))
                    NoteCountWatch.WarnOnDisagreement(logger, request.Mix, p.ChartId, p.Judgements, noteCount,
                        p.IsBroken, p.IsStageBroken);
                // A stage break is broken by definition and never scored: the running number the
                // site prints for one is not a chart score, and the plate is null on any break.
                var isBroken = p.IsBroken || p.IsStageBroken;
                var score = p.IsStageBroken ? null : p.Score;
                return new ScoreJournalEntry(p.PlayedAt, request.Source, request.UserId, p.ChartId,
                    score, BestAttemptPolicy.PlateFor(isBroken, p.Plate), isBroken, request.Mix,
                    request.SessionId, PhoenixComboSolver.WithMaxCombo(p.Judgements, score, noteCount), false,
                    IsStageBroken: p.IsStageBroken);
            })
            .ToArray();

        await journal.AppendObservations(entries, cancellationToken);

        // The limbo board reads exactly these rows, so it goes stale exactly here. Evicted AFTER
        // the write, which is why this is the hook rather than ScoreImportCompletedEvent — that
        // one is published before the rows it describes exist, and only for official imports
        // (docs/design/limbo-leaderboard.md §5). Remove on an absent key is a no-op, so no chart
        // needs checking against the flag set first.
        foreach (var chartId in entries.Select(e => e.ChartId).Distinct())
            cache.Remove(LedgerCacheKeys.LimboBoard(request.Mix, chartId));
    }
}
