using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetPhoenixRecordHandler : IRequestHandler<GetPhoenixRecordQuery, RecordedPhoenixScore?>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPhoenixRecordRepository _records;

    public GetPhoenixRecordHandler(ICurrentUserAccessor currentUser, IPhoenixRecordRepository records)
    {
        _currentUser = currentUser;
        _records = records;
    }

    public async Task<RecordedPhoenixScore?> Handle(GetPhoenixRecordQuery request, CancellationToken cancellationToken)
    {
        return await _records.GetRecordedScore(_currentUser.User.Id, request.ChartId, cancellationToken);
    }
}