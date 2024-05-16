using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record DeleteMatchLinkCommand(Guid TournamentId, Name FromName, Name ToName) : IRequest
    {
    }
}
