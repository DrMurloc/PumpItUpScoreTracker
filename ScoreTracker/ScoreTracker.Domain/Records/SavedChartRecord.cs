using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Records;

public sealed record SavedChartRecord(ChartListType ListType, Guid ChartId)
{
}