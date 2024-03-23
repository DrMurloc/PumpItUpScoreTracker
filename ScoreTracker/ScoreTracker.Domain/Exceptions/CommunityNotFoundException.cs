namespace ScoreTracker.Domain.Exceptions
{
    public sealed class CommunityNotFoundException : Exception
    {
        public CommunityNotFoundException() : base("Community was not found.")
        {
        }
    }
}
