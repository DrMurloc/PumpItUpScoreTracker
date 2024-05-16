using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Commands
{
    public sealed record UpdateMatchCommand(Guid TournamentId, MatchView NewView) : IRequest
    {
    }
}
