namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class InvalidNameException : Exception
{
    public InvalidNameException(string reason) : base($"Invalid name: {reason}")
    {
    }
}
