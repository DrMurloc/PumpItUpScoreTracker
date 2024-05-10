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
        //995010
        //Batches up score posts to reduce noise
        var fireAt = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        if (!_fireAt.ContainsKey(user.User.Id))
        {
            _newCharts[user.User.Id] = new HashSet<Guid>();
            _upscoreCharts[user.User.Id] = new ConcurrentDictionary<Guid, PhoenixScore>();
            await scheduler.SchedulePublish(fireAt + TimeSpan.FromSeconds(5),
                new TryFireScoreMessage(user.User.Id),
                cancellationToken);
        }

        _fireAt[user.User.Id] = fireAt;

        if (isNewScore) _newCharts[user.User.Id].Add(request.ChartId);

        if (isUpscore && !_newCharts[user.User.Id].Contains(request.ChartId))
            _upscoreCharts[user.User.Id][request.ChartId] = existing!.Score!.Value;
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

        var newChartIds = _newCharts.TryGetValue(context.Message.UserId, out var newChart)
            ? newChart.ToArray()
            : Array.Empty<Guid>();

        var upscoredChartIds = _upscoreCharts.TryGetValue(context.Message.UserId, out var chart)
            ? chart
                .ToDictionary(kv => kv.Key, kv => (int)kv.Value)
            : new Dictionary<Guid, int>();

        await bus.Publish(
            new PlayerScoreUpdatedEvent(context.Message.UserId, newChartIds, upscoredChartIds),
            context.CancellationToken);
        _fireAt.TryRemove(context.Message.UserId, out _);
        _upscoreCharts.TryRemove(context.Message.UserId, out _);
        _newCharts.TryRemove(context.Message.UserId, out _);
    }
}