namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class UserNotFoundException : Exception
{
    public UserNotFoundException(Guid userId) : base($"User '{userId}' was not found.")
    {
    }
}
