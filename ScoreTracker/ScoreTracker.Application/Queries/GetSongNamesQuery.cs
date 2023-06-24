using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetSongNamesQuery(MixEnum Mix) : IRequest<IEnumerable<Name>>
{
}