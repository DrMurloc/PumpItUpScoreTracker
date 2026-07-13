namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class TournamentInviteInvalidException : Exception
{
    public TournamentInviteInvalidException() : base("This invite link is invalid or has expired.")
    {
    }
}
