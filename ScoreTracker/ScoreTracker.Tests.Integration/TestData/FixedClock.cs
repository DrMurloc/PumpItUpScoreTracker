using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Tests.Integration.TestData;

/// <summary>
///     A stub clock for repositories that stamp a time. Hand-rolled rather than a Moq setup because
///     this project's doubles sit at the wire or in DI, and one property does not earn a proxy.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class FixedClock : IDateTimeOffsetAccessor
{
    private readonly DateTimeOffset _now;

    public FixedClock(DateTimeOffset now)
    {
        _now = now;
    }

    public DateTimeOffset Now => _now;
}
