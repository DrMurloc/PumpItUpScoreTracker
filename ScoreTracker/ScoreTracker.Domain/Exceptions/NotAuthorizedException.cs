namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class NotAuthorizedException : Exception
{
    public NotAuthorizedException(string action) : base($"User is not authorized to {action}.")
    {
    }
}
