using System;
using System.Globalization;
using System.Linq;
using ScoreTracker.Domain.Services;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class DailyStepLimboPolicyTests
{
    [Theory]
    [InlineData(2026, 1)]
    [InlineData(2026, 28)]
    [InlineData(2026, 52)]
    [InlineData(2025, 40)]
    [InlineData(2027, 15)]
    public void ExactlyOneLimboDayPerIsoWeek(int isoYear, int isoWeek)
    {
        var monday = ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday);
        var limboDays = Enumerable.Range(0, 7)
            .Select(offset => new DateTimeOffset(monday.AddDays(offset), TimeSpan.Zero))
            .Count(DailyStepLimboPolicy.IsLimboDay);

        Assert.Equal(1, limboDays);
    }

    [Fact]
    public void LimboWeekdayVariesAcrossWeeks()
    {
        var distinctDays = Enumerable.Range(1, 52)
            .Select(week => DailyStepLimboPolicy.LimboDayOfWeek(2026, week))
            .Distinct()
            .Count();

        // A fixed weekday — or a slow one-day-a-week march — would collapse this; a healthy
        // spread hits most of the seven days across a year.
        Assert.True(distinctDays >= 5, $"Expected a spread of Limbo weekdays, saw {distinctDays}");
    }

    [Fact]
    public void LimboDayOfWeekIsDeterministic()
    {
        Assert.Equal(DailyStepLimboPolicy.LimboDayOfWeek(2026, 28),
            DailyStepLimboPolicy.LimboDayOfWeek(2026, 28));
    }
}
