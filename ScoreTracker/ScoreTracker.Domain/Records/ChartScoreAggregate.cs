namespace ScoreTracker.Domain.Records
{
    /// <summary>Per-chart population counts: players with a scored record, and how many of them passed.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record ChartScoreAggregate(Guid ChartId, int Count, int PassCount = 0)
    {
    }
}
