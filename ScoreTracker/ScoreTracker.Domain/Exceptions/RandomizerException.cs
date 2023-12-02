namespace ScoreTracker.Domain.Exceptions
{
    public sealed class RandomizerException : Exception
    {
        public RandomizerException(string message) : base(message)
        {
        }
    }
}
