using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Views;

[ExcludeFromCodeCoverage]
public sealed record MatchScoring(Name MatchName, int[] PlacingPoints)
{
}
