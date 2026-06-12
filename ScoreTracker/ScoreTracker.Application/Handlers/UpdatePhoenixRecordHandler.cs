using MassTransit;
using MediatR;
using ScoreTracker.Application.Messages;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers;

public sealed class UpdatePhoenixRecordHandler(IPhoenixRecordRepository records,
        ICurrentUserAccessor user,
        IDateTimeOffsetAccessor dateTimeOffset,
        IBus bus,
        IMessageScheduler scheduler,
        IPlayerScoreBatchAccumulator batches)
    : IRequestHandler<UpdatePhoenixBestAttemptCommand>,
        IConsumer<UpdatePhoenixRecordHandler.TryFireScoreMessage>,
        IConsumer<FlushOverdueScoreBatches>
{
    public async Task Handle(UpdatePhoenixBestAttemptCommand request, CancellationToken cancellationToken)
    {
        var existing = await records.GetRecordedScore(user.User.Id, request.ChartId, cancellationToken);
        var score = request.Score;
        var plate = request.Plate;
        var isBroken = request.IsBroken;
        if (request.KeepBestStats && existing?.Score != null && request.Score < existing?.Score)
            score = existing.Score;

        if (request.KeepBestStats && existing?.Plate != null && request.Plate < existing?.Plate)
            plate = existing.Plate;

        if (request.KeepBestStats && !(existing?.IsBroken ?? true) && request.IsBroken)
            isBroken = false;

        await records.UpdateBestAttempt(user.User.Id,
            new RecordedPhoenixScore(request.ChartId, score, plate, isBroken,
                dateTimeOffset.Now), cancellationToken);
        var isNewScore = (existing?.IsBroken ?? true) && !request.IsBroken;
        var isUpscore = existing?.Score != null && request.Score != null && existing.Score < request.Score;
        if (!isNewScore && !isUpscore) return;

        // Batch up score posts to reduce noise. AddToBatch atomically creates-or-extends
        // the user's batch; only schedule a drain when this call created the batch.
        var fireAt = dateTimeOffset.Now.UtcDateTime + TimeSpan.FromMinutes(2);
        PhoenixScore? upscoredFrom = isUpscore ? existing!.Score!.Value : null;
        if (batches.AddToBatch(user.User.Id, fireAt, request.ChartId, isNewScore, upscoredFrom))
        {
            await scheduler.SchedulePublish(fireAt + TimeSpan.FromSeconds(5),
                new TryFireScoreMessage(user.User.Id),
                cancellationToken);
        }
    }

    public sealed record ScheduleScoreMessage(Guid UserId, Guid[] ChartIds);

    public sealed record TryFireScoreMessage(Guid UserId);

    public async Task Consume(ConsumeContext<TryFireScoreMessage> context)
    {
        var fireAt = batches.GetFireAt(context.Message.UserId);
        if (fireAt is null) return; // batch already drained by a concurrent TryFire/flush
        if (dateTimeOffset.Now.UtcDateTime < fireAt.Value)
        {
            // Reschedule to the moving target plus a tiny buffer — using a +2min retry
            // would compound on every reschedule and starve active players.
            await scheduler.SchedulePublish(fireAt.Value + TimeSpan.FromSeconds(5),
                new TryFireScoreMessage(context.Message.UserId),
                context.CancellationToken);
            return;
        }

        var batch = batches.TakeBatch(context.Message.UserId);
        if (batch is null) return; // raced another drain
        if (batch.NewChartIds.Length == 0 && batch.UpscoredChartIds.Count == 0) return;
        await bus.Publish(
            new PlayerScoreUpdatedEvent(context.Message.UserId, batch.NewChartIds, batch.UpscoredChartIds),
            context.CancellationToken);
    }

    // Safety net for batches whose scheduled TryFireScoreMessage was lost
    // (in-memory MassTransit transport drops in-flight messages on restart).
    public async Task Consume(ConsumeContext<FlushOverdueScoreBatches> context)
    {
        var now = dateTimeOffset.Now.UtcDateTime;
        foreach (var entry in batches.Dump())
        {
            if (entry.FireAt > now) continue;
            var batch = batches.TakeBatch(entry.UserId);
            if (batch is null) continue;
            if (batch.NewChartIds.Length == 0 && batch.UpscoredChartIds.Count == 0) continue;
            await bus.Publish(
                new PlayerScoreUpdatedEvent(entry.UserId, batch.NewChartIds, batch.UpscoredChartIds),
                context.CancellationToken);
        }
    }
}
