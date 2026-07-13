using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>Removes a staff member's role. Head TO or site admin only; never yourself.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record RemoveTournamentRoleCommand(Guid TournamentId, Guid UserId) : IRequest
    {
    }
}
