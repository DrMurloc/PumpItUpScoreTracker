using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetRecommendedChartsQuery
        (ChartType? ChartType, int LevelOffset) : IQuery<IEnumerable<ChartRecommendation>>
    {
    }
}
