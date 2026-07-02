using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Catalog.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartsBySongQuery(MixEnum Mix, Name SongName) : IQuery<IEnumerable<Chart>>
{
}
