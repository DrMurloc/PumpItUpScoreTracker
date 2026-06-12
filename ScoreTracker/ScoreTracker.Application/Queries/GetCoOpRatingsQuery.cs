using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetCoOpRatingsQuery : IQuery<IEnumerable<CoOpRating>>
    {
    }
}
