using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands;

public sealed record RateChartDifficultyCommand
    (Guid ChartId, DifficultyAdjustment Rating) : IRequest<ChartDifficultyRatingRecord>
{
}