using ScoreTracker.Domain.Records;

namespace ScoreTracker.ScoreLedger.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetAllChartScoreAggregatesQuery : IQuery<IEnumerable<ChartScoreAggregate>>
    {
    }
}
