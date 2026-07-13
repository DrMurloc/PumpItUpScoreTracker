using MediatR;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    /// <summary>
    ///     Redeems an invite token for the current user and returns the tournament id (the
    ///     redirect target). Grants the invite's role only when the user holds none — an
    ///     assistant link never downgrades a Head TO. Throws TournamentInviteInvalidException
    ///     on unknown or expired tokens.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record RedeemTournamentRoleInviteCommand(Guid Token) : IRequest<Guid>
    {
    }
}
