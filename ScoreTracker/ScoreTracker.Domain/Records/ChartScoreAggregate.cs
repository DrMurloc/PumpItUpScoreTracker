namespace ScoreTracker.Domain.Records
{
    /// <summary>Per-chart population counts: players with a scored record, how many passed, and how many hold the PG.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record ChartScoreAggregate(Guid ChartId, int Count, int PassCount = 0, int PgCount = 0)
    {
    }
}
