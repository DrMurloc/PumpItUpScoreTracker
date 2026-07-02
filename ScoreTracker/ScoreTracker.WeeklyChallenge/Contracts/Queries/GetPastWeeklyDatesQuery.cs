namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>The dates of archived weekly boards.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetPastWeeklyDatesQuery : IQuery<IEnumerable<DateTimeOffset>>
{
}
