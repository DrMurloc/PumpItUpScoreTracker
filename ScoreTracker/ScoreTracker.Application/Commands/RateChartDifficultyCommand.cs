using MediatR;
using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Application.Commands;

public sealed record RateChartDifficultyCommand(Guid ChartId, DifficultyAdjustment Rating) : IRequest<double>
{
}