using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetChartQuery(MixEnum Mix, Name SongName, DifficultyLevel Level, ChartType Type) : IRequest<Chart?>
{
}