using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Catalog.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartsBySongQuery(MixEnum Mix, Name SongName) : IQuery<IEnumerable<Chart>>
{
}
