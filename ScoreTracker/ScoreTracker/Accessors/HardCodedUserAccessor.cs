using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Accessors;

public sealed class HardCodedUserAccessor : ICurrentUserAccessor
{
    public Guid UserId => Guid.Empty;
}