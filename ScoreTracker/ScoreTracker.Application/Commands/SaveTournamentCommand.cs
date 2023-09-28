using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Commands
{
    public sealed record SaveTournamentCommand(TournamentConfiguration Tournament) : IRequest

    {
    }
}