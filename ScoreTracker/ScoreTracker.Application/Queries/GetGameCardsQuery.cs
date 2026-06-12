using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetGameCardsQuery(string Username, string Password) : IQuery<IEnumerable<GameCardRecord>>
    {
    }
}
