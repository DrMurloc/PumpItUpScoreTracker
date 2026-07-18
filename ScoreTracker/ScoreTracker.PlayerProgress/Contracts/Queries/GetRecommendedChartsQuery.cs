using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetRecommendedChartsQuery
        (ChartType? ChartType, int LevelOffset, MixEnum Mix = MixEnum.Phoenix,
            IReadOnlySet<RecommendationCategory>? Categories = null,
            RecommendationLevelWindow? LevelWindow = null,
            HotStreakOptions? HotStreak = null)
        : IQuery<IEnumerable<ChartRecommendation>>
    {
    }
}
