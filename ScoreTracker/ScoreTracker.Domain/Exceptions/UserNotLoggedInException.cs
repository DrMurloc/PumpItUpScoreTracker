namespace ScoreTracker.Domain.Exceptions;

public sealed class UserNotLoggedInException : Exception
{
    public UserNotLoggedInException() : base("User is not logged in.")
    {
    }
}