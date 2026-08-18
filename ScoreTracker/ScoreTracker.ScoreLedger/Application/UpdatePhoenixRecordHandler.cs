using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Events;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class UpdatePhoenixRecordHandler(IPhoenixRecordRepository records,
        ICurrentUserAccessor user,
        IDateTimeOffsetAccessor dateTimeOffset,
        IBus bus,
        IMessageScheduler scheduler,
        IPlayerScoreBatchAccumulator batches,
        IScoreJournalRepository journal,
        IScoreSessionRepository sessions,
        IMemoryCache cache,
        IChartRepository charts,
        ILogger<UpdatePhoenixRecordHandler> logger)
    : IRequestHandler<UpdatePhoenixBestAttemptCommand>,
        IConsumer<UpdatePhoenixRecordHandler.TryFireScoreCommand>,
        IConsumer<FlushOverdueScoreBatchesCommand>
{
    public async Task Handle(UpdatePhoenixBestAttemptCommand request, CancellationToken cancellationToken)
    {
        // A legacy mix stores a letter grade in BestAttempt, not a score in PhoenixRecord.
        // Accepting one here writes a real row into a store no legacy read path consults,
        // wearing a 1,000,000-scale label an era score does not mean — so it is refused
        // before a session is opened rather than corrected afterwards.
        if (request.Mix.UsesLegacyScoring())
            throw new WrongScoringModelException(request.Mix, "letter grade");

        // Every submission — no-ops included — extends the session envelope: activity
        // keeps the session alive even when nothing lands.
        var (sessionId, isNewSession) = batches.GetOrExtendSession(request.Mix, user.User.Id, request.Source,
            dateTimeOffset.Now, request.SessionId);
        // Recorded once, on the submission that minted it. Counts and the activity window
        // follow at batch drain — an import posts thousands of scores and must not post
        // thousands of updates.
        if (isNewSession)
            await sessions.Open(sessionId, user.User.Id, request.Mix, request.Source, null, null,
                dateTimeOffset.Now, cancellationToken);
        // A break with nothing hit never enters the system, from any source.
        if (BestAttemptPolicy.IsWalkOff(request.IsBroken, request.Score, request.Judgements)) return;

        // A stage break is never a best, whatever the opt-in said — but it is a play, and a dated
        // one is journaled as such: it is what "attempts before this clear" counts. Undated ones
        // have no play key and are dropped; the site dates every card, so that is a manual entry
        // claiming a stage break, which has nothing to say.
        if (!BestAttemptPolicy.CanBeRecord(request.IsStageBroken))
        {
            if (request.RecordedAt is { } playedAt)
                await journal.AppendObservations(new[]
                {
                    // The combo is re-solved rather than carried: it is a function of the score,
                    // and a stage break has none — so whatever a caller sent is dropped instead of
                    // stored against a play that cannot support one.
                    new ScoreJournalEntry(playedAt, request.Source, user.User.Id, request.ChartId, null, null, true,
                        request.Mix, sessionId, PhoenixComboSolver.WithMaxCombo(request.Judgements, null, null),
                        false, IsStageBroken: true)
                }, cancellationToken);
            return;
        }

        var existing = await records.GetRecordedScore(request.Mix, user.User.Id, request.ChartId, cancellationToken);
        // The game awards no plate on a failed stage, so a broken attempt carries none.
        var plate = BestAttemptPolicy.PlateFor(request.IsBroken, request.Plate);

        // KeepBestStats means "apply the best-attempt policy" — the acquisition sources, which
        // may only ever raise a record. Without it the submission is authoritative and
        // overwrites: the manual routes, and the only way a personal best can decrease.
        if (request.KeepBestStats &&
            !BestAttemptPolicy.Beats(existing, request.Score, plate, request.IsBroken)) return;

        // Progress only: a submission that leaves the best attempt unchanged is noise
        // (the import deliberately re-scrapes past its cutoff, so repeats are expected),
        // not history — it must not touch the record, the journal, or RecordedDate.
        var recordChanged = existing == null || request.Score != existing.Score || plate != existing.Plate ||
                            request.IsBroken != existing.IsBroken;
        if (!recordChanged) return;

        // Judgements decompose one specific play's score. Reaching this line means the result
        // changed, so a different play produced it — the previous play's counts describe the
        // old result and are dropped rather than mislabeled onto the new one. They travel with
        // their solved combo, and against the catalog's count they are the tripwire that says
        // the catalog has drifted — a log line, never a refusal.
        var noteCount = request.Judgements == null
            ? null
            : await NoteCountWatch.NoteCountFor(charts, request.Mix, request.ChartId, cancellationToken);
        NoteCountWatch.WarnOnDisagreement(logger, request.Mix, request.ChartId, request.Judgements, noteCount,
            request.IsBroken, false);
        var judgements = PhoenixComboSolver.WithMaxCombo(request.Judgements, request.Score, noteCount);
        // The site's saved timestamp, when it supplied one, is the truthful record/journal
        // time; the clock only stamps submissions the site never dated.
        var recordedAt = request.RecordedAt ?? dateTimeOffset.Now;

        await records.UpdateBestAttempt(request.Mix, user.User.Id,
            new RecordedPhoenixScore(request.ChartId, request.Score, plate, request.IsBroken,
                recordedAt, request.Source, judgements), cancellationToken);
        // The journal is the record's history: it gets the resulting best-attempt state,
        // exactly and only when that state changes.
        await journal.Append(new ScoreJournalEntry(recordedAt, request.Source, user.User.Id,
                request.ChartId, request.Score, plate, request.IsBroken, request.Mix, sessionId, judgements),
            cancellationToken);
        // A first pass on a limbo chart is journaled here rather than as an observation, and it may
        // be the player's only one — so this path evicts too. Remove on an absent key is a no-op,
        // which is why nothing checks whether the chart is flagged first.
        cache.Remove(LedgerCacheKeys.LimboBoard(request.Mix, request.ChartId));
        var isNewScore = (existing?.IsBroken ?? true) && !request.IsBroken;
        var isUpscore = existing?.Score != null && request.Score != null && existing.Score < request.Score;
        if (!isNewScore && !isUpscore) return;

        // Batch up score posts to reduce noise. AddToBatch atomically creates-or-extends
        // the (user, mix) batch; only schedule a drain when this call created the batch.
        var fireAt = dateTimeOffset.Now.UtcDateTime + ScoreBatchPolicy.HoldWindow;
        PhoenixScore? upscoredFrom = isUpscore ? existing!.Score!.Value : null;
        if (batches.AddToBatch(request.Mix, user.User.Id, fireAt, request.ChartId, isNewScore, upscoredFrom,
                sessionId))
        {
            await scheduler.SchedulePublish(fireAt + ScoreBatchPolicy.DrainBuffer,
                new TryFireScoreCommand(user.User.Id, request.Mix),
                cancellationToken);
        }
    }

    public sealed record ScheduleScoreMessage(Guid UserId, Guid[] ChartIds);

    public sealed record TryFireScoreCommand(Guid UserId, MixEnum Mix);

    public async Task Consume(ConsumeContext<TryFireScoreCommand> context)
    {
        var fireAt = batches.GetFireAt(context.Message.Mix, context.Message.UserId);
        if (fireAt is null) return; // batch already drained by a concurrent TryFire/flush
        if (dateTimeOffset.Now.UtcDateTime < fireAt.Value)
        {
            // Reschedule to the moving target plus a tiny buffer — using a full hold window as
            // the retry would compound on every reschedule and starve active players.
            await scheduler.SchedulePublish(fireAt.Value + ScoreBatchPolicy.DrainBuffer,
                new TryFireScoreCommand(context.Message.UserId, context.Message.Mix),
                context.CancellationToken);
            return;
        }

        var batch = batches.TakeBatch(context.Message.Mix, context.Message.UserId);
        if (batch is null) return; // raced another drain
        if (batch.NewChartIds.Length == 0 && batch.UpscoredChartIds.Count == 0) return;
        await PublishScoreEvents(context.Message.UserId, batch, context.CancellationToken);
    }

    // Publishes the fat PlayerScoresUpdatedEvent contract event (C11/C22).

    private async Task PublishScoreEvents(Guid userId, PendingScoreBatch batch,
        CancellationToken cancellationToken)
    {
        var involved = batch.NewChartIds.Concat(batch.UpscoredChartIds.Keys).ToHashSet();
        var bests = (await records.GetRecordedScores(batch.Mix, userId, cancellationToken) ?? [])
            .Where(r => involved.Contains(r.ChartId))
            .ToDictionary(r => r.ChartId);
        // Shared with the restart replay, so a recovered batch announces itself in exactly the
        // shape a live one would have.
        var changes = ScoreChangeAssembler.Build(batch, bests);
        // The drain is the session's checkpoint: one write per batch rather than one per score.
        if (batch.SessionId is { } sessionId)
            await sessions.Touch(sessionId, dateTimeOffset.Now, batch.NewChartIds.Length,
                batch.UpscoredChartIds.Count, cancellationToken);
        await bus.Publish(
            PlayerScoresUpdatedEvent.Create(dateTimeOffset.Now, userId, batch.Mix, changes, batch.SessionId),
            cancellationToken);
    }

    /// <summary>
    ///     Drains batches that are past their deadline and still sitting in the accumulator.
    ///     <para>
    ///         Covers one specific failure: the scheduled <see cref="TryFireScoreCommand" /> never
    ///         arrived, inside a process that is still running. The scores are safe either way —
    ///         they were written on submission — but everything derived from them (highlights,
    ///         folder lamps, ratings, titles, the session card) hangs off the drain, so a lost
    ///         schedule strands all of it while the import reports success.
    ///     </para>
    ///     <para>
    ///         It does NOT cover a restart: the accumulator is in memory, so a batch caught by one
    ///         is already gone by the time this runs and there is nothing here to find. That half
    ///         is the session replay in OfficialMirror, on the same message.
    ///     </para>
    /// </summary>
    public async Task Consume(ConsumeContext<FlushOverdueScoreBatchesCommand> context)
    {
        // Past the deadline and past the buffer the scheduled drain gets. Inside that slack a
        // drain may still be in flight; beyond it the schedule is not late, it is not coming.
        var claimBefore = dateTimeOffset.Now.UtcDateTime - ScoreBatchPolicy.DrainBuffer;
        foreach (var due in batches.TakeDueBatches(claimBefore))
        {
            if (due.Batch.NewChartIds.Length == 0 && due.Batch.UpscoredChartIds.Count == 0) continue;
            await PublishScoreEvents(due.UserId, due.Batch, context.CancellationToken);
        }

        // The journal-replay half runs only now, never alongside. Both halves are in scope for the
        // very same sessions on the tick this job exists for, and a session stays unprocessed until
        // its capture chain ends — so a replay racing this drain would find the session still
        // unmarked and announce the batch a second time. Publishing the second half from the end of
        // the first is what makes their "disjoint" claim true rather than merely likely.
        await bus.Publish(new OverdueScoreBatchesFlushedEvent(dateTimeOffset.Now),
            context.CancellationToken);
    }
}
