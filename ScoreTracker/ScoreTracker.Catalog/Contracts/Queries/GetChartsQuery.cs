using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Catalog.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartsQuery
(MixEnum Mix, DifficultyLevel? Level = null, ChartType? Type = null,
    IEnumerable<Guid>? ChartIds = null) : IQuery<IEnumerable<Chart>>
{
}
