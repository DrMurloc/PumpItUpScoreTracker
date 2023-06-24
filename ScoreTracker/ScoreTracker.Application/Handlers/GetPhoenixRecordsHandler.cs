using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;

namespace ScoreTracker.Application.Handlers;

public sealed class
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
        if (!await _userAccess.HasAccessTo(request.UserId, cancellationToken))
            return Array.Empty<RecordedPhoenixScore>();

        return await _records.GetRecordedScores(request.UserId, cancellationToken);
    }
}