using ScoreTracker.Domain.Records;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>The current week's challenge board charts.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetWeeklyChartsQuery : IQuery<IEnumerable<WeeklyTournamentChart>>
{
}
