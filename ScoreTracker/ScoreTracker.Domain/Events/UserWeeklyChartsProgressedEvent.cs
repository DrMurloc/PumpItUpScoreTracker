namespace ScoreTracker.Domain.Events
{
    public sealed record UserWeeklyChartsProgressedEvent(Guid UserId, Guid ChartId, int Score,
        string Plate, bool IsBroken, int Place)
    {
    }
}
