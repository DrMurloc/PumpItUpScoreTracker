using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetChartScoreRankingsQuery
        (IEnumerable<Guid> ChartIds) : IRequest<IDictionary<Guid, ScoreRankingRecord>>
    {
    }
}
