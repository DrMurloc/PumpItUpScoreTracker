using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPlayerScoreQualityQuery
        (DifficultyLevel Level, ChartType ChartType) : IQuery<IDictionary<Guid, ScoreRankingRecord>>
    {
    }
}
