using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Views;

[ExcludeFromCodeCoverage]
public sealed record MatchScoring(Name MatchName, int[] PlacingPoints)
{
}
