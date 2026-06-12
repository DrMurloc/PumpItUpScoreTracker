using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetMyCoOpRatingQuery(Guid ChartId) : IQuery<IDictionary<int, DifficultyLevel>?>
    {
    }
}
