using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetBestChartAttemptQuery
    (Name SongName, ChartType ChartType, DifficultyLevel Level) : IRequest<BestChartAttempt>
{
}