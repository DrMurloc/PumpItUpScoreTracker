using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Undoes one session: drop its plays, then rebuild every chart it touched from the plays
///     that remain. Deleting alone would leave the session's scores standing as the record,
///     which is the opposite of undoing it.
///     Independence falls out of the rebuild for free — a chart a later session also improved
///     keeps the later score, because that play is still there to be replayed.
/// </summary>
internal sealed class UndoScoreSessionHandler(
        IScoreSessionRepository sessions,
        IScoreJournalRepository journal,
        IPhoenixRecordRepository records,
        IDateTimeOffsetAccessor dateTime,
        IBus bus)
    : IRequestHandler<UndoScoreSessionCommand, ScoreSessionUndoResult>
{
    public async Task<ScoreSessionUndoResult> Handle(UndoScoreSessionCommand request,
        CancellationToken cancellationToken)
    {
        var session = await sessions.Get(request.SessionId, cancellationToken);
        // Ownership is checked here rather than trusted from the caller: the session id is a
        // bare Guid on a public contract.
        if (session == null || session.UserId != request.UserId)
            return new ScoreSessionUndoResult(ScoreSessionUndoOutcome.NotFound);
        if (!session.CanUndo) return new ScoreSessionUndoResult(ScoreSessionUndoOutcome.TooOld);

        var removed = await journal.GetSessionEntries(request.UserId, request.SessionId, cancellationToken);
        var chartIds = removed.Select(e => e.ChartId).Distinct().ToArray();

        await journal.DeleteSession(request.UserId, request.SessionId, cancellationToken);

        var restored = 0;
        var cleared = 0;
        var survivors = chartIds.Length == 0
            ? Array.Empty<ScoreJournalEntry>()
            : (await journal.GetChartHistories(request.UserId, chartIds, cancellationToken)).ToArray();
        foreach (var chartId in chartIds)
        {
            var best = SessionUndoReplay.BestOf(survivors.Where(e => e.ChartId == chartId));
            if (best == null)
            {
                // Nothing came before, so there is nothing to put back — the chart returns to
                // never having been played.
                await records.DeleteRecord(session.Mix, request.UserId, chartId, cancellationToken);
                cleared++;
                continue;
            }

            await records.UpdateBestAttempt(session.Mix, request.UserId,
                new RecordedPhoenixScore(chartId, best.Score, best.Plate, best.IsBroken, best.OccurredAt,
                    best.Source, best.Judgements), cancellationToken);
            restored++;
        }

        await sessions.Delete(request.SessionId, cancellationToken);
        await bus.Publish(new ScoreSessionUndoneEvent(request.UserId, request.SessionId, session.Mix),
            cancellationToken);
        // Stats, Pumbility and titles recompute through the pipeline that already exists.
        await bus.Publish(
            PlayerScoresUpdatedEvent.Create(dateTime.Now, request.UserId, session.Mix,
                Array.Empty<PlayerScoresUpdatedEvent.ScoreChange>()),
            cancellationToken);

        return new ScoreSessionUndoResult(ScoreSessionUndoOutcome.Undone, restored, cleared);
    }
}
