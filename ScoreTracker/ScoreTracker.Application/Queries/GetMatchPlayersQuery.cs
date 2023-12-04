using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetMatchPlayersQuery : IRequest<IEnumerable<MatchPlayer>>
    {
    }
}
