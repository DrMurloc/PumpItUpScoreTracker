using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetCoOpRatingsQuery : IQuery<IEnumerable<CoOpRating>>
    {
    }
}
