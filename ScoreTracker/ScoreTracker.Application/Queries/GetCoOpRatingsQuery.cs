using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetCoOpRatingsQuery : IRequest<IEnumerable<CoOpRating>>
    {
    }
}
