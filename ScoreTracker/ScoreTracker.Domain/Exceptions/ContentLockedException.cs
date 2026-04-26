namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class ContentLockedException : Exception
{
    public ContentLockedException() : base("This account is content-locked and cannot change its username.")
    {
    }
}
