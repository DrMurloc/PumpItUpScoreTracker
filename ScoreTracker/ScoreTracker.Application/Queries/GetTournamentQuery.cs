using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetTournamentQuery(Guid TournamentId) : IRequest<TournamentConfiguration>
    {
    }
}
