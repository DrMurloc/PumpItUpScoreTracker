using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Catalog.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetSongNamesQuery(MixEnum Mix) : IQuery<IEnumerable<Name>>
{
}
