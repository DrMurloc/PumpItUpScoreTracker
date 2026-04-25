namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record UserWeeklyChartsProgressedEvent(Guid UserId, Guid ChartId, int Score,
        string Plate, bool IsBroken, int Place)
    {
    }
}
