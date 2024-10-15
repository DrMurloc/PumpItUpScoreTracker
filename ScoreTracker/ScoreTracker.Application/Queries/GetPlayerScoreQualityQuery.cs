using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetPlayerScoreQualityQuery
        (DifficultyLevel Level, ChartType ChartType) : IRequest<IDictionary<Guid, double>>
    {
    }
}
