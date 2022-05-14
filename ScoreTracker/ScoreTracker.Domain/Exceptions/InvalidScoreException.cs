namespace ScoreTracker.Domain.Exceptions;

public sealed class InvalidScoreException : Exception
{
    public InvalidScoreException(string reason) : base($"Invalid Score: {reason}")
    {
    }
}