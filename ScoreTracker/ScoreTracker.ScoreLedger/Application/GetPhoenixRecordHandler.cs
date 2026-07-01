using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class GetPhoenixRecordHandler : IRequestHandler<GetPhoenixRecordQuery, RecordedPhoenixScore?>
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