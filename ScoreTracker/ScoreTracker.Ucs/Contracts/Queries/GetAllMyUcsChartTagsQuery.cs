namespace ScoreTracker.Ucs.Contracts.Queries;

/// <summary>Every UCS chart tag the current user has applied, across all charts.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetAllMyUcsChartTagsQuery : IQuery<IEnumerable<UserChartTag>>
{
}
