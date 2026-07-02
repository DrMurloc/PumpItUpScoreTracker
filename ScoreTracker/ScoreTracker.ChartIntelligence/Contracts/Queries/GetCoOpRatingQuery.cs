using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetCoOpRatingQuery(Guid ChartId) : IQuery<CoOpRating?>
    {
    }
}
