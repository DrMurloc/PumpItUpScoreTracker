using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
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
        IMemoryCache cache)
    : IRequestHandler<UpdatePhoenixBestAttemptCommand>,
        IConsumer<UpdatePhoenixRecordHandler.TryFireScoreCommand>
{
    public async Task Handle(UpdatePhoenixBestAttemptCommand request, CancellationToken cancellationToken)
    {
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
        // A stage break with nothing judged never enters the system, from any source.
        if (BestAttemptPolicy.IsWalkOff(request.IsBroken, request.Score, request.Judgements)) return;

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
        // old result and are dropped rather than mislabeled onto the new one.
        var judgements = request.Judgements;
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
        var changes = involved.Select(chartId =>
        {
            var best = bests.GetValueOrDefault(chartId);
            return new PlayerScoresUpdatedEvent.ScoreChange(
                chartId,
                IsNewPass: !batch.UpscoredChartIds.ContainsKey(chartId),
                OldScore: batch.UpscoredChartIds.TryGetValue(chartId, out var old) ? old : null,
                NewScore: best?.Score,
                Plate: best?.Plate?.ToString(),
                IsBroken: best?.IsBroken ?? false);
        }).ToArray();
        // The drain is the session's checkpoint: one write per batch rather than one per score.
        if (batch.SessionId is { } sessionId)
            await sessions.Touch(sessionId, dateTimeOffset.Now, batch.NewChartIds.Length,
                batch.UpscoredChartIds.Count, cancellationToken);
        await bus.Publish(
            PlayerScoresUpdatedEvent.Create(dateTimeOffset.Now, userId, batch.Mix, changes, batch.SessionId),
            cancellationToken);
    }
}
