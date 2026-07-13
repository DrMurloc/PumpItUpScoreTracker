using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>Revokes an invite link. Head TO or site admin only.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record DeleteTournamentRoleInviteCommand(Guid TournamentId, Guid Token) : IRequest
    {
    }
}
