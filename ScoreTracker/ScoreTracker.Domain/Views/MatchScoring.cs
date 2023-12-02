using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Views;

public sealed record MatchScoring(Name MatchName, int[] PlacingPoints)
{
}