using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.EventCompetition.Contracts.Queries
{
    /// <summary>Invite-link management view — Head TO or site admin only.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetTournamentRoleInvitesQuery(Guid TournamentId)
        : IQuery<IEnumerable<TournamentRoleInviteRecord>>
    {
    }
}
