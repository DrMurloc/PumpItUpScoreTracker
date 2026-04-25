namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class InvalidDifficultyLevelException : Exception
{
    public InvalidDifficultyLevelException(string reason) : base($"Invalid Difficulty Level: {reason}")
    {
    }
}
