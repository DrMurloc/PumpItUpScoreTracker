using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Exceptions
{
    [ExcludeFromCodeCoverage]
    public sealed class CommunityAlreadyExistsException : Exception
    {
        public CommunityAlreadyExistsException(Name name) : base($"Community {name} already exists")
        {
        }
    }
}
