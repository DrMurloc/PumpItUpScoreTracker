using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Catalog.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartQuery(MixEnum Mix, Name SongName, DifficultyLevel Level, ChartType Type) : IQuery<Chart?>
{
}
