using ScoreTracker.Domain.Models;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetPhoenixRecordQuery(Guid ChartId) : IQuery<RecordedPhoenixScore?>
{
}
