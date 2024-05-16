using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record DrawChartsCommand(Guid TournamentId, Name MatchName) : IRequest
    {
    }
}
