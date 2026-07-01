namespace ScoreTracker.Ucs.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUcsChartLeaderboardQuery(Guid ChartId) : IQuery<IEnumerable<UcsLeaderboardEntry>>
{
}
