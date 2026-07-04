using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetAllChartScoreAggregatesQuery(MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IEnumerable<ChartScoreAggregate>>
    {
    }
}
