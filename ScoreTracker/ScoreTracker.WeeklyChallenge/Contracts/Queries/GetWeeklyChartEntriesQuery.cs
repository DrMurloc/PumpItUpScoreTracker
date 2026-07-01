using ScoreTracker.Domain.Records;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>This week's submitted entries, optionally filtered to one chart.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetWeeklyChartEntriesQuery(Guid? ChartId = null)
    : IQuery<IEnumerable<WeeklyTournamentEntry>>
{
}
