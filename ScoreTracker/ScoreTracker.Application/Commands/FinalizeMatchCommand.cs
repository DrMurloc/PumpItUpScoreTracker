using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record FinalizeMatchCommand(Guid TournamentId, Name MatchName) : IRequest
    {
    }
}
