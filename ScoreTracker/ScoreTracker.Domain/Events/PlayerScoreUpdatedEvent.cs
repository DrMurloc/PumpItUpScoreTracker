namespace ScoreTracker.Domain.Events;

public sealed record PlayerScoreUpdatedEvent
    (Guid UserId, Guid[] NewChartIds, IDictionary<Guid, int> UpscoredChartIds)
{
}