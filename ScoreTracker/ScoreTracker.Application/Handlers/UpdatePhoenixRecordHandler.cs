using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class UpdatePhoenixRecordHandler : IRequestHandler<UpdatePhoenixBestAttemptCommand>
{
    private readonly IDateTimeOffsetAccessor _dateTimeOffset;
    private readonly IPhoenixRecordRepository _records;
    private readonly ICurrentUserAccessor _user;

    public UpdatePhoenixRecordHandler(
        IPhoenixRecordRepository records,
        ICurrentUserAccessor user,
        IDateTimeOffsetAccessor dateTimeOffset)
    {
        _records = records;
        _user = user;
        _dateTimeOffset = dateTimeOffset;
    }

    public async Task<Unit> Handle(UpdatePhoenixBestAttemptCommand request, CancellationToken cancellationToken)
    {
        await _records.UpdateBestAttempt(_user.User.Id,
            new RecordedPhoenixScore(request.ChartId, request.Score, request.Plate, request.IsBroken,
                _dateTimeOffset.Now), cancellationToken);
        return Unit.Value;
    }
}