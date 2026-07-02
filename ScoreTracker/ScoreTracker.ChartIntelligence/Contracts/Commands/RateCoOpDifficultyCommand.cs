using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record RateCoOpDifficultyCommand
        (MixEnum mix, Guid ChartId, IDictionary<int, DifficultyLevel>? Ratings) : IRequest<CoOpRating?>
    {
    }
}
