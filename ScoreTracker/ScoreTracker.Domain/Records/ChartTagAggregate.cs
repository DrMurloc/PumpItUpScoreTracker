using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record ChartTagAggregate(Guid ChartId, Name Tag, int Count)
{
}
