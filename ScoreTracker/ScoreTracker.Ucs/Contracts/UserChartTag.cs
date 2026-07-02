using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Ucs.Contracts;

[ExcludeFromCodeCoverage]
public sealed record UserChartTag(Guid ChartId, Guid UserId, Name Tag)
{
}
