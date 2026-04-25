namespace ScoreTracker.Domain.Exceptions
{
    [ExcludeFromCodeCoverage]
    public sealed class RandomizerException : Exception
    {
        public RandomizerException(string message) : base(message)
        {
        }
    }
}
