using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record DrawChartsCommand(Guid TournamentId, Name MatchName) : IRequest
    {
    }
}
