namespace ScoreTracker.Domain.Records
{
    public sealed record ChartScoreAggregate(Guid ChartId, int Count)
    {
    }
}