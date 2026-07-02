using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SaveTournamentCommand(TournamentConfiguration Tournament) : IRequest

    {
    }
}
