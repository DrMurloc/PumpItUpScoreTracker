using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetMatchPlayersQuery(Guid TournamentId) : IRequest<IEnumerable<MatchPlayer>>
    {
    }
}
