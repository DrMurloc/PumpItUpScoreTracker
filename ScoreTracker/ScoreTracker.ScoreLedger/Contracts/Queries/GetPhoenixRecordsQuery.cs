using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetPhoenixRecordsQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IEnumerable<RecordedPhoenixScore>>
{
}
