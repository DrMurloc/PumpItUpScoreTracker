using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record UpdateMatchCommand(Guid TournamentId, MatchView NewView) : IRequest
    {
    }
}
