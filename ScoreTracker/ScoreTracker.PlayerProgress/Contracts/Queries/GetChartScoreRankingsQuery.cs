using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetChartScoreRankingsQuery
        (IEnumerable<Guid> ChartIds, MixEnum Mix = MixEnum.Phoenix) : IQuery<IDictionary<Guid, ScoreRankingRecord>>
    {
    }
}
