using System;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Tests.TestHelpers;

internal static class FakeDateTime
{
    public static Mock<IDateTimeOffsetAccessor> At(DateTimeOffset now)
    {
        var mock = new Mock<IDateTimeOffsetAccessor>();
        mock.SetupGet(d => d.Now).Returns(now);
        return mock;
    }

    public static Mock<IDateTimeOffsetAccessor> At(int year, int month, int day,
        int hour = 0, int minute = 0, int second = 0)
    {
        return At(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));
    }
}
