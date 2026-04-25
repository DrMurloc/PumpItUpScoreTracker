namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record ChartScoreAggregate(Guid ChartId, int Count)
    {
    }
}
