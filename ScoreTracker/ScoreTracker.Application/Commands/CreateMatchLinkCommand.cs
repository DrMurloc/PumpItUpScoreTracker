using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record CreateMatchLinkCommand(Guid TournamentId, MatchLink MatchLink) : IRequest
    {
    }
}
