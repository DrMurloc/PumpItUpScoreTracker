using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record ResolveMatchCommand(Guid TournamentId, Name MatchName) : IRequest
    {
    }
}
