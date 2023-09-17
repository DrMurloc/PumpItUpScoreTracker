using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetTournamentQuery(Guid TournamentId) : IRequest<TournamentConfiguration>
    {
    }
}