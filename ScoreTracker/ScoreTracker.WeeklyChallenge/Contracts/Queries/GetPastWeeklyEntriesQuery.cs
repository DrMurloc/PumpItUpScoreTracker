using ScoreTracker.Domain.Records;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>The archived entries for a past week's board.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetPastWeeklyEntriesQuery(DateTimeOffset Date)
    : IQuery<IEnumerable<WeeklyTournamentEntry>>
{
}
