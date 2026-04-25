using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record SavedChartRecord(ChartListType ListType, Guid ChartId)
{
}
