using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUcsChartTagsQuery : IQuery<IEnumerable<ChartTagAggregate>>
{
}
