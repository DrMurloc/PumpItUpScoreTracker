using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Accessors;

public class DateTimeOffsetAccessor : IDateTimeOffsetAccessor
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}