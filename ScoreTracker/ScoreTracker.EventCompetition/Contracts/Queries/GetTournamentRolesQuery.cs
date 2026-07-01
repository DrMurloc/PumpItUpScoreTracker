using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.EventCompetition.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetTournamentRolesQuery(Guid TournamentId) : IQuery<IEnumerable<UserTournamentRole>>
    {
    }
}
