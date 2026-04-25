using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record UpdateMatchScoresCommand
        (Guid TournamentId, Name MatchName, Name Player, int ChartIndex, PhoenixScore NewScore) : IRequest
    {
    }
}
