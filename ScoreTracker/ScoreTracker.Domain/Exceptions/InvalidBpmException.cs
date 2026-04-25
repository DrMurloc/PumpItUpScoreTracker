namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class InvalidBpmException : Exception
{
    public InvalidBpmException(string reason) : base($"Invalid BPM: {reason}")
    {
    }
}
