using MediatR;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class
    GetPhoenixRecordsHandler : IRequestHandler<GetPhoenixRecordsQuery, IEnumerable<RecordedPhoenixScore>>
{
    private readonly IPhoenixRecordRepository _records;
    private readonly IUserAccessService _userAccess;

    public GetPhoenixRecordsHandler(IUserAccessService userAccess, IPhoenixRecordRepository records)
    {
        _userAccess = userAccess;
        _records = records;
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> Handle(GetPhoenixRecordsQuery request,
        CancellationToken cancellationToken)
    {
        return await _records.GetRecordedScores(request.UserId, cancellationToken);
    }
}