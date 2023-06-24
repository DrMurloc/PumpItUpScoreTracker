namespace ScoreTracker.Domain.Exceptions;

public sealed class InvalidStepCountException : Exception
{
    public InvalidStepCountException(string reason) : base($"Invalid StepCount: {reason}")
    {
    }
}