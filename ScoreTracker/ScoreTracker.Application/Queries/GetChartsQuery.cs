using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetChartsQuery
(MixEnum Mix, DifficultyLevel? Level = null, ChartType? Type = null,
    IEnumerable<Guid>? ChartIds = null) : IRequest<IEnumerable<Chart>>
{
}