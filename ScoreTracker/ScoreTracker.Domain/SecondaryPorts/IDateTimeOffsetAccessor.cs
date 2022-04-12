namespace ScoreTracker.Domain.SecondaryPorts;

public interface IDateTimeOffsetAccessor
{
    DateTimeOffset Now { get; }
}