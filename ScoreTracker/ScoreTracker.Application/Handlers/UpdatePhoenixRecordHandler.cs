using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class UpdatePhoenixRecordHandler(IPhoenixRecordRepository records,
        ICurrentUserAccessor user,
        IDateTimeOffsetAccessor dateTimeOffset,
        IBus bus,
        IMessageScheduler scheduler,
        IPlayerScoreBatchAccumulator batches)
    : IRequestHandler<UpdatePhoenixBestAttemptCommand>,
        IConsumer<UpdatePhoenixRecordHandler.TryFireScoreMessage>,
        IConsumer<FlushOverdueScoreBatchesEvent>
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

        //Batches up score posts to reduce noise
        var fireAt = dateTimeOffset.Now.UtcDateTime + TimeSpan.FromMinutes(2);
        if (batches.RegisterFireAt(user.User.Id, fireAt))
        {
            await scheduler.SchedulePublish(fireAt + TimeSpan.FromSeconds(5),
                new TryFireScoreMessage(user.User.Id),
                cancellationToken);
        }

        if (isNewScore) batches.RecordNewChart(user.User.Id, request.ChartId);

        if (isUpscore)
            batches.RecordUpscoreIfNotNew(user.User.Id, request.ChartId, existing!.Score!.Value);
    }

    public sealed record ScheduleScoreMessage(Guid UserId, Guid[] ChartIds);

    public sealed record TryFireScoreMessage(Guid UserId);

    public async Task Consume(ConsumeContext<TryFireScoreMessage> context)
    {
        var fireAt = batches.GetFireAt(context.Message.UserId);
        if (dateTimeOffset.Now.UtcDateTime < fireAt)
        {
            await scheduler.SchedulePublish(fireAt + TimeSpan.FromMinutes(2),
                new TryFireScoreMessage(context.Message.UserId),
                context.CancellationToken);
            return;
        }

        var batch = batches.TakeBatch(context.Message.UserId);
        await bus.Publish(
            new PlayerScoreUpdatedEvent(context.Message.UserId, batch.NewChartIds, batch.UpscoredChartIds),
            context.CancellationToken);
    }

    // Safety net for batches whose scheduled TryFireScoreMessage was lost
    // (in-memory MassTransit transport drops in-flight messages on restart).
    public async Task Consume(ConsumeContext<FlushOverdueScoreBatchesEvent> context)
    {
        var now = dateTimeOffset.Now.UtcDateTime;
        foreach (var entry in batches.Dump())
        {
            if (entry.FireAt > now) continue;
            var batch = batches.TakeBatch(entry.UserId);
            if (batch.NewChartIds.Length == 0 && batch.UpscoredChartIds.Count == 0) continue;
            await bus.Publish(
                new PlayerScoreUpdatedEvent(entry.UserId, batch.NewChartIds, batch.UpscoredChartIds),
                context.CancellationToken);
        }
    }
}
