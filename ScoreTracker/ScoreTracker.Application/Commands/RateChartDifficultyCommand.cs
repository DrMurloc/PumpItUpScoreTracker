using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record RateChartDifficultyCommand
    (MixEnum Mix, Guid ChartId, DifficultyAdjustment Rating) : IRequest<ChartDifficultyRatingRecord>
{
}
