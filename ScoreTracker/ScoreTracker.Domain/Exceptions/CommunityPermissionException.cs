namespace ScoreTracker.Domain.Exceptions
{
    [ExcludeFromCodeCoverage]
    public sealed class CommunityPermissionException : Exception
    {
        public CommunityPermissionException(string reason) : base($"Community action not permitted: {reason}")
        {
        }
    }
}
