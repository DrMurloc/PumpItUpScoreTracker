namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class InvalidSeasonIdException : Exception
{
    public InvalidSeasonIdException(string reason) : base($"Invalid season: {reason}")
    {
    }
}
