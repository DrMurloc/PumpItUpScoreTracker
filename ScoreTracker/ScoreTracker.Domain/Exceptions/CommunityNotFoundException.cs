namespace ScoreTracker.Domain.Exceptions
{
    [ExcludeFromCodeCoverage]
    public sealed class CommunityNotFoundException : Exception
    {
        public CommunityNotFoundException() : base("Community was not found.")
        {
        }
    }
}
