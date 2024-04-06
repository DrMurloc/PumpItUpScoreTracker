using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class UpdatePhoenixRecordHandler : IRequestHandler<UpdatePhoenixBestAttemptCommand>
{
    private readonly IDateTimeOffsetAccessor _dateTimeOffset;
    private readonly IPhoenixRecordRepository _records;
    private readonly ICurrentUserAccessor _user;
    private readonly IBus _bus;

    public UpdatePhoenixRecordHandler(
        IPhoenixRecordRepository records,
        ICurrentUserAccessor user,
        IDateTimeOffsetAccessor dateTimeOffset,
        IBus bus)
    {
        _records = records;
        _user = user;
        _dateTimeOffset = dateTimeOffset;
        _bus = bus;
    }

    public async Task Handle(UpdatePhoenixBestAttemptCommand request, CancellationToken cancellationToken)
    {
        await _records.UpdateBestAttempt(_user.User.Id,
            new RecordedPhoenixScore(request.ChartId, request.Score, request.Plate, request.IsBroken,
                _dateTimeOffset.Now), cancellationToken);
        if (!request.SkipEvent)
            await _bus.Publish(new PlayerScoreUpdatedEvent(_user.User.Id, new[] { request.ChartId }),
                cancellationToken);
    }
}