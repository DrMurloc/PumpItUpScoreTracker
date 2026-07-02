using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Ucs.Contracts;

[ExcludeFromCodeCoverage]
public sealed record ChartTagAggregate(Guid ChartId, Name Tag, int Count)
{
}
