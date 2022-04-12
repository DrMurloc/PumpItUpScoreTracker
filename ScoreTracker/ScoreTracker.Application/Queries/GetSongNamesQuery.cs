using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries;

public sealed record GetSongNamesQuery : IRequest<IEnumerable<Name>>
{
}