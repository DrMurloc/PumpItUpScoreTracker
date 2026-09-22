using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Tests.Api;

/// <summary>A fixed clock for the controllers that validate a caller's timestamps against now.</summary>
internal static class ApiTestClock
{
    public static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public static IDateTimeOffsetAccessor Accessor { get; } = new Fixed();

    private sealed class Fixed : IDateTimeOffsetAccessor
    {
        public DateTimeOffset Now => ApiTestClock.Now;
    }
}
