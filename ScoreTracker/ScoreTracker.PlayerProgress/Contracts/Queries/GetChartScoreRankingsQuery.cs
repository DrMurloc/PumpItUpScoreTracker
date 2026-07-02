using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetChartScoreRankingsQuery
        (IEnumerable<Guid> ChartIds) : IQuery<IDictionary<Guid, ScoreRankingRecord>>
    {
    }
}
