using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetTournamentRolesQuery(Guid TournamentId) : IRequest<IEnumerable<UserTournamentRole>>
    {
    }
}
