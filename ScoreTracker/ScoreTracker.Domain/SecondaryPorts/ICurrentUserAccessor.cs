namespace ScoreTracker.Domain.SecondaryPorts;

public interface ICurrentUserAccessor
{
    Guid UserId { get; }
}