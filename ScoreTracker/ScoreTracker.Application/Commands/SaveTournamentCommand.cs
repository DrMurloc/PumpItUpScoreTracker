using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SaveTournamentCommand(TournamentConfiguration Tournament) : IRequest

    {
    }
}
