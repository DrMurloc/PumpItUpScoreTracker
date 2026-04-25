namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class UserNotLoggedInException : Exception
{
    public UserNotLoggedInException() : base("User is not logged in.")
    {
    }
}
