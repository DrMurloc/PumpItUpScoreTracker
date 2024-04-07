using System.Collections.Concurrent;
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
        IMessageScheduler scheduler)
    : IRequestHandler<UpdatePhoenixBestAttemptCommand>,
        IConsumer<UpdatePhoenixRecordHandler.TryFireScoreMessage>
{
    public async Task Handle(UpdatePhoenixBestAttemptCommand request, CancellationToken cancellationToken)
    {
        await records.UpdateBestAttempt(user.User.Id,
            new RecordedPhoenixScore(request.ChartId, request.Score, request.Plate, request.IsBroken,
                dateTimeOffset.Now), cancellationToken);
        if (!request.SkipEvent)
        {
            //Batches up score posts to reduce noise
            var fireAt = DateTime.UtcNow + TimeSpan.FromMinutes(2);
            if (_changedCharts.TryGetValue(user.User.Id, out var chartSet))
            {
                if (!chartSet.Contains(request.ChartId))
                    chartSet.Add(request.ChartId);

                _fireAt[user.User.Id] = fireAt;
                return;
            }

            _changedCharts[user.User.Id] = new HashSet<Guid>(new[] { request.ChartId });

            _fireAt[user.User.Id] = fireAt;
            await scheduler.SchedulePublish(fireAt + TimeSpan.FromSeconds(5),
                new TryFireScoreMessage(user.User.Id),
                cancellationToken);
        }
    }

    public sealed record ScheduleScoreMessage(Guid UserId, Guid[] ChartIds);

    public sealed record TryFireScoreMessage(Guid UserId);

    private static readonly ConcurrentDictionary<Guid, ISet<Guid>> _changedCharts = new();

    private static readonly ConcurrentDictionary<Guid, DateTime> _fireAt = new();

    public async Task Consume(ConsumeContext<TryFireScoreMessage> context)
    {
        if (DateTime.UtcNow < _fireAt[context.Message.UserId])
        {
            await scheduler.SchedulePublish(_fireAt[context.Message.UserId] + TimeSpan.FromSeconds(5),
                new TryFireScoreMessage(context.Message.UserId),
                context.CancellationToken);
            return;
        }

        await bus.Publish(
            new PlayerScoreUpdatedEvent(context.Message.UserId, _changedCharts[context.Message.UserId].ToArray()),
            context.CancellationToken);
        _fireAt.TryRemove(context.Message.UserId, out _);
        _changedCharts.TryRemove(context.Message.UserId, out _);
    }
}