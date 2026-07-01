namespace ScoreTracker.Ucs.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUcsChartsQuery : IQuery<IEnumerable<UcsChart>>
{
}
