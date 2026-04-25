namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class InvalidScoreException : Exception
{
    public InvalidScoreException(string reason) : base($"Invalid Score: {reason}")
    {
    }
}
