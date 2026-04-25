namespace ScoreTracker.Domain.Events;

[ExcludeFromCodeCoverage]
public sealed record PlayerScoreUpdatedEvent
    (Guid UserId, Guid[] NewChartIds, IDictionary<Guid, int> UpscoredChartIds)
{
}
