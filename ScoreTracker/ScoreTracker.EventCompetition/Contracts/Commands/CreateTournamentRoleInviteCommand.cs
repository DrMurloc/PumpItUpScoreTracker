using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>
    ///     Mints a role-carrying invite link token. Head Tournament Organizer (or site
    ///     admin) only. Returns the token that goes in the shareable URL.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record CreateTournamentRoleInviteCommand(Guid TournamentId, TournamentRole Role,
        DateTimeOffset? ExpiresAt) : IRequest<Guid>
    {
    }
}
