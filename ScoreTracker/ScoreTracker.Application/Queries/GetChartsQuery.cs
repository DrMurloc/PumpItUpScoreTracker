using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetChartsQuery
    (DifficultyLevel? Level = null, ChartType? Type = null) : IRequest<IEnumerable<Chart>>
{
}