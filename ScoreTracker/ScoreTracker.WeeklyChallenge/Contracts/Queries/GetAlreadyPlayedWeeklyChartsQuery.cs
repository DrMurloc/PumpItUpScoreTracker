namespace ScoreTracker.WeeklyChallenge.Contracts.Queries;

/// <summary>Chart ids that have already appeared on past weekly boards (rotation exclusions).</summary>
[ExcludeFromCodeCoverage]
public sealed record GetAlreadyPlayedWeeklyChartsQuery : IQuery<IEnumerable<Guid>>
{
}
