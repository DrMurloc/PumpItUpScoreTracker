namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record UcsLeaderboardPlacedEvent(Guid UserId, Guid ChartId)
    {
    }
}
