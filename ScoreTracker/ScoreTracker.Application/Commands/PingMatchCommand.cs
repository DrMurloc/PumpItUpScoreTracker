using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record PingMatchCommand(Guid TournamentId, Name MatchName) : IRequest
    {
    }
}
