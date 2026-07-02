using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPlayerScoreQualityQuery
        (DifficultyLevel Level, ChartType ChartType) : IQuery<IDictionary<Guid, ScoreRankingRecord>>
    {
    }
}
