using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetRecommendedChartsQuery
        (ChartType? ChartType, int LevelOffset) : IRequest<IEnumerable<ChartRecommendation>>
    {
    }
}
