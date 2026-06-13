using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

/// <summary>Tags the current user has applied to one UCS chart.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyUcsChartTagsQuery(Guid ChartId) : IQuery<IEnumerable<Name>>
{
}
