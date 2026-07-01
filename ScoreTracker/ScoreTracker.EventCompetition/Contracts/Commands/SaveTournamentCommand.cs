using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SaveTournamentCommand(TournamentConfiguration Tournament) : IRequest

    {
    }
}
