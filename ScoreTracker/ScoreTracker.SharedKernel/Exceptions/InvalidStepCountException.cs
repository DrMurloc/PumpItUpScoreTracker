namespace ScoreTracker.Domain.Exceptions;

[ExcludeFromCodeCoverage]
public sealed class InvalidStepCountException : Exception
{
    public InvalidStepCountException(string reason) : base($"Invalid StepCount: {reason}")
    {
    }
}
