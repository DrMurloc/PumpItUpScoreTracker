namespace ScoreTracker.Domain.Exceptions
{
    [ExcludeFromCodeCoverage]
    public sealed class DeniedFromCommunityException : Exception
    {
        public DeniedFromCommunityException(string reason) : base($"Not allowed into community: {reason}")
        {
        }
    }
}
