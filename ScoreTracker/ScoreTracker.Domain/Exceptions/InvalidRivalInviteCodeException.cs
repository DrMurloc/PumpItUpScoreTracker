namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class InvalidRivalInviteCodeException : Exception
{
    public InvalidRivalInviteCodeException(string reason) : base($"That invite code isn't valid: {reason}")
    {
    }
}
