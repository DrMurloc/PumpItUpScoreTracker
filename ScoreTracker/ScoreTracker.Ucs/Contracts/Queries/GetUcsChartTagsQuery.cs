namespace ScoreTracker.Ucs.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUcsChartTagsQuery : IQuery<IEnumerable<ChartTagAggregate>>
{
}
