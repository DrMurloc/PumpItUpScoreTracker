using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

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
        ISeasonReader seasons,
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
            // Same mix only. A returning song carries ONE ChartId across Phoenix and Phoenix 2,
            // so a chart's history spans both — and replaying it unfiltered writes the other
            // mix's score in as this mix's record. Undoing a Phoenix 2 session then leaves your
            // Phoenix 1 score standing as your Phoenix 2 best, which a later import cannot
            // correct: acquisition may only RAISE a record, so the wrong, higher number sticks
            // and every re-imported play reads as "Played". Classify and the session rebuild
            // both already carry this filter; this was the one replay that did not.
            var best = SessionUndoReplay.BestOf(
                survivors.Where(e => e.ChartId == chartId && e.Mix == session.Mix));
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

        await ReplaySeasons(session.Mix, request.UserId, chartIds, survivors, cancellationToken);

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

    /// <summary>
    ///     The same replay again, once per open season, with that season's window applied
    ///     (docs/design/seasons.md §4.1): the season's best on a chart is the best surviving play
    ///     inside its window, and no surviving play in the window means the chart returns to unplayed
    ///     for that season. Sealed seasons are skipped — nothing writes one (D13), so an undo cannot
    ///     reach back past a seal any more than an import can.
    ///     <para>
    ///         A row the counting rule seated on the raise clause rather than on its date — an upscore
    ///         wearing a pre-season stamp — is not reconstructible here, because whether a card raised
    ///         a record is import-time knowledge the journal does not keep. Replaying by window is the
    ///         rule the design chose knowing that; the nightly rollup recomputes standings from
    ///         whatever bests survive either way.
    ///     </para>
    /// </summary>
    private async Task ReplaySeasons(MixEnum mix, Guid userId, IReadOnlyList<Guid> chartIds,
        IReadOnlyList<ScoreJournalEntry> survivors, CancellationToken cancellationToken)
    {
        if (chartIds.Count == 0 || !mix.HasSeasons()) return;
        var open = (await seasons.GetSeasons(cancellationToken)).Where(s => !s.IsSealed).ToArray();
        if (open.Length == 0) return;

        foreach (var season in open)
        foreach (var chartId in chartIds)
        {
            // Official imports only, the same rule the writer applies (D4): the replay may not seat a
            // hand-typed score in a season's pool. Source has to be filtered BEFORE BestOf rather than
            // after, because BestOf treats a manual row as authoritative — it overwrites rather than
            // competes, which is right for the all-time record and wrong for a season.
            var best = SessionUndoReplay.BestOf(survivors.Where(e =>
                e.ChartId == chartId && e.Mix == mix && season.Holds(e.OccurredAt)
                && e.Source == ScoreJournalEntry.OfficialImportSource));
            if (best != null)
            {
                await records.UpdateBestAttempt(mix, userId,
                    new RecordedPhoenixScore(chartId, best.Score, best.Plate, best.IsBroken, best.OccurredAt,
                        best.Source, best.Judgements), season.Id, cancellationToken);
                continue;
            }

            // Nothing in the window survives. That means "delete" only for a row the window could have
            // produced: a row the raise clause seated carries a card date OUTSIDE the season by
            // construction, so a window replay was never going to find its evidence, and removing it
            // would be the replay destroying what it cannot rebuild. Leave that one standing; the
            // nightly rollup prices whatever is actually there either way.
            var stored = await records.GetRecordedScore(mix, userId, chartId, season.Id, cancellationToken);
            if (stored != null && !season.Holds(stored.RecordedDate)) continue;
            await records.DeleteRecord(mix, userId, chartId, season.Id, cancellationToken);
        }
    }
}
