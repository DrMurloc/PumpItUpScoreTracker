using MediatR;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetMatchQuery(Guid TournamentId, Name MatchName) : IRequest<MatchView>
    {
    }
}
