using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetMyCoOpRatingQuery(Guid ChartId) : IQuery<IDictionary<int, DifficultyLevel>?>
    {
    }
}
