using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record RateCoOpDifficultyCommand
        (MixEnum mix, Guid ChartId, IDictionary<int, DifficultyLevel>? Ratings) : IRequest<CoOpRating?>
    {
    }
}