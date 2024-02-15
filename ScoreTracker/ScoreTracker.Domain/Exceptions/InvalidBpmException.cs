namespace ScoreTracker.Domain.Exceptions;

public sealed class InvalidBpmException : Exception
{
    public InvalidBpmException(string reason) : base($"Invalid BPM: {reason}")
    {
    }
}