using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetPhoenixRecordQuery(Guid ChartId, MixEnum Mix = MixEnum.Phoenix) : IQuery<RecordedPhoenixScore?>
{
}
