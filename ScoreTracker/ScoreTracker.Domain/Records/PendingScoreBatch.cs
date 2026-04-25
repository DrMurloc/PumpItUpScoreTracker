namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record PendingScoreBatch(Guid[] NewChartIds, IDictionary<Guid, int> UpscoredChartIds)
{
}
