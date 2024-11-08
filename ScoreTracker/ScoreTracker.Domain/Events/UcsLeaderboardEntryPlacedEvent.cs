namespace ScoreTracker.Domain.Events
{
    public sealed record UcsLeaderboardPlacedEvent(Guid UserId, Guid ChartId)
    {
    }
}
