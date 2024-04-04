namespace ScoreTracker.Domain.Events;

public sealed record PlayerScoreUpdatedEvent
    (Guid UserId, Guid[] ChartIds)
{
}