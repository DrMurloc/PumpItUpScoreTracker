using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record DrawChartsCommand(Guid TournamentId, Name MatchName) : IRequest
    {
    }
}
