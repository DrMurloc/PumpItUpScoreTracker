using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetGameCardsQuery(string Username, string Password) : IRequest<IEnumerable<GameCardRecord>>
    {
    }
}
