using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetCoOpRatingQuery(Guid ChartId) : IRequest<CoOpRating?>
    {
    }
}
