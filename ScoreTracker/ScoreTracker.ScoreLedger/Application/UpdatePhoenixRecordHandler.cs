using MassTransit;
using MediatR;
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
        IScoreJournalRepository journal)
    : IRequestHandler<UpdatePhoenixBestAttemptCommand>,
        IConsumer<UpdatePhoenixRecordHandler.TryFireScoreCommand>,
        IConsumer<FlushOverdueScoreBatchesCommand>
{
    public async Task Handle(UpdatePhoenixBestAttemptCommand request, CancellationToken cancellationToken)
    {
        var existing = await records.GetRecordedScore(request.Mix, user.User.Id, request.ChartId, cancellationToken);
        var score = request.Score;
        var plate = request.Plate;
        var isBroken = request.IsBroken;
        if (request.KeepBestStats && existing?.Score != null && request.Score < existing?.Score)
            score = existing.Score;

        if (request.KeepBestStats && existing?.Plate != null && request.Plate < existing?.Plate)
            plate = existing.Plate;

        if (request.KeepBestStats && !(existing?.IsBroken ?? true) && request.IsBroken)
            isBroken = false;

        // Progress only: a submission that leaves the best attempt unchanged is noise
        // (the import deliberately re-scrapes past its cutoff, so repeats are expected),
        // not history — it must not touch the record, the journal, or RecordedDate.
        var recordChanged = existing == null || score != existing.Score || plate != existing.Plate ||
                            isBroken != existing.IsBroken;
        if (!recordChanged) return;

        await records.UpdateBestAttempt(request.Mix, user.User.Id,
            new RecordedPhoenixScore(request.ChartId, score, plate, isBroken,
                dateTimeOffset.Now), cancellationToken);
        // The journal is the record's history: it gets the resulting best-attempt state,
        // exactly and only when that state changes.
        await journal.Append(new ScoreJournalEntry(dateTimeOffset.Now, request.Source, user.User.Id,
            request.ChartId, score, plate, isBroken, request.Mix), cancellationToken);
        var isNewScore = (existing?.IsBroken ?? true) && !isBroken;
        var isUpscore = existing?.Score != null && score != null && existing.Score < score;
        if (!isNewScore && !isUpscore) return;

        // Batch up score posts to reduce noise. AddToBatch atomically creates-or-extends
        // the (user, mix) batch; only schedule a drain when this call created the batch.
        var fireAt = dateTimeOffset.Now.UtcDateTime + TimeSpan.FromMinutes(2);
        PhoenixScore? upscoredFrom = isUpscore ? existing!.Score!.Value : null;
        if (batches.AddToBatch(request.Mix, user.User.Id, fireAt, request.ChartId, isNewScore, upscoredFrom))
        {
            await scheduler.SchedulePublish(fireAt + TimeSpan.FromSeconds(5),
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
            // Reschedule to the moving target plus a tiny buffer — using a +2min retry
            // would compound on every reschedule and starve active players.
            await scheduler.SchedulePublish(fireAt.Value + TimeSpan.FromSeconds(5),
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
        await bus.Publish(PlayerScoresUpdatedEvent.Create(dateTimeOffset.Now, userId, batch.Mix, changes),
            cancellationToken);
    }

    // Safety net for batches whose scheduled TryFireScoreCommand was lost
    // (in-memory MassTransit transport drops in-flight messages on restart).
    public async Task Consume(ConsumeContext<FlushOverdueScoreBatchesCommand> context)
    {
        var now = dateTimeOffset.Now.UtcDateTime;
        foreach (var entry in batches.Dump())
        {
            if (entry.FireAt > now) continue;
            var batch = batches.TakeBatch(entry.Mix, entry.UserId);
            if (batch is null) continue;
            if (batch.NewChartIds.Length == 0 && batch.UpscoredChartIds.Count == 0) continue;
            await PublishScoreEvents(entry.UserId, batch, context.CancellationToken);
        }
    }
}
