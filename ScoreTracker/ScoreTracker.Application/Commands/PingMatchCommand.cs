using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record PingMatchCommand(Guid TournamentId, Name MatchName) : IRequest
    {
    }
}
