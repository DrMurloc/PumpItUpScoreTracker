namespace ScoreTracker.Domain.Exceptions;

public sealed class InvalidNameException : Exception
{
    public InvalidNameException(string reason) : base($"Invalid name: {reason}")
    {
    }
}