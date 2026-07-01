using ScoreTracker.Domain.Records;

namespace ScoreTracker.ScoreLedger.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPhoenixScoresForChartQuery(Guid ChartId) : IQuery<IEnumerable<UserPhoenixScore>>
    {
    }
}
