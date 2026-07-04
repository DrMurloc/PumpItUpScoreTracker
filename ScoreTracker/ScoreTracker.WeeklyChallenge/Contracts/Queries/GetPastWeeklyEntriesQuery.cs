using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>The archived entries for a past week's board on a mix.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetPastWeeklyEntriesQuery(DateTimeOffset Date, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IEnumerable<WeeklyTournamentEntry>>
{
}
