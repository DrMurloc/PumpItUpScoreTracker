using System.Collections.Concurrent;
using MassTransit;
using MediatR;
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
        IMessageScheduler scheduler)
    : IRequestHandler<UpdatePhoenixBestAttemptCommand>,
        IConsumer<UpdatePhoenixRecordHandler.TryFireScoreMessage>
{
    public async Task Handle(UpdatePhoenixBestAttemptCommand request, CancellationToken cancellationToken)
    {
        var existing = await records.GetRecordedScore(user.User.Id, request.ChartId, cancellationToken);

        await records.UpdateBestAttempt(user.User.Id,
            new RecordedPhoenixScore(request.ChartId, request.Score, request.Plate, request.IsBroken,
                dateTimeOffset.Now), cancellationToken);
        //Batches up score posts to reduce noise
        var fireAt = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        if ((existing?.IsBroken ?? true) && !request.IsBroken)
        {
            _fireAt[user.User.Id] = fireAt;
            if (_newCharts.TryGetValue(user.User.Id, out var chartSet))
            {
                if (!chartSet.Contains(request.ChartId))
                    chartSet.Add(request.ChartId);

                return;
            }

            _newCharts[user.User.Id] = new HashSet<Guid>(new[] { request.ChartId });


            await scheduler.SchedulePublish(fireAt + TimeSpan.FromSeconds(5),
                new TryFireScoreMessage(user.User.Id),
                cancellationToken);
        }
        else if (existing?.Score != null && request.Score != null && existing.Score < request.Score)
        {
            _fireAt[user.User.Id] = fireAt;
            if (_upscoreCharts.TryGetValue(user.User.Id, out var upscoreSet))
            {
                if (!upscoreSet.ContainsKey(request.ChartId))
                    upscoreSet[request.ChartId] = existing.Score.Value;
                return;
            }

            _upscoreCharts[user.User.Id] = new ConcurrentDictionary<Guid, PhoenixScore>();
            _upscoreCharts[user.User.Id][request.ChartId] = existing.Score.Value;

            await scheduler.SchedulePublish(fireAt + TimeSpan.FromSeconds(5),
                new TryFireScoreMessage(user.User.Id),
                cancellationToken);
        }
    }

    public sealed record ScheduleScoreMessage(Guid UserId, Guid[] ChartIds);

    public sealed record TryFireScoreMessage(Guid UserId);

    private static readonly ConcurrentDictionary<Guid, IDictionary<Guid, PhoenixScore>> _upscoreCharts = new();
    private static readonly ConcurrentDictionary<Guid, ISet<Guid>> _newCharts = new();

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

        //895238
        var newChartIds = _newCharts.TryGetValue(context.Message.UserId, out var newChart)
            ? newChart
            : Array.Empty<Guid>().ToHashSet();

        var upscoredChartIds = _upscoreCharts.TryGetValue(context.Message.UserId, out var chart)
            ? chart.Where(kv => !newChartIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => (int)kv.Value)
            : new Dictionary<Guid, int>();
        await bus.Publish(
            new PlayerScoreUpdatedEvent(context.Message.UserId, newChartIds.ToArray(), upscoredChartIds),
            context.CancellationToken);
        _fireAt.TryRemove(context.Message.UserId, out _);
        _upscoreCharts.TryRemove(context.Message.UserId, out _);
        _newCharts.TryRemove(context.Message.UserId, out _);
    }
}