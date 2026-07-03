namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class InvalidAccountMergeException : Exception
{
    public InvalidAccountMergeException(string message) : base(message)
    {
    }
}
