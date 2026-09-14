using System;
using ScoreTracker.Seasons.Domain;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class SeasonCalendarTests
{
    private static readonly SeasonId Summer2026 = SeasonId.From(2026, 3);
    private static readonly SeasonId Fall2026 = SeasonId.From(2026, 4);

    [Fact]
    public void AQuarterTurnsOverAtMidnightUtcMinusFiveLikeMarchOfMurlocs()
    {
        // 04:59:59Z on 1 October is still 23:59:59 on 30 September in the boundary offset.
        Assert.Equal(Summer2026, SeasonCalendar.QuarterAt(new DateTimeOffset(2026, 10, 1, 4, 59, 59, TimeSpan.Zero)));
        Assert.Equal(Fall2026, SeasonCalendar.QuarterAt(new DateTimeOffset(2026, 10, 1, 5, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void AWindowRunsFromTheFirstMidnightToTheLastSecondOfTheQuarter()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, SeasonCalendar.Offset), SeasonCalendar.StartOf(Fall2026));
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 59, SeasonCalendar.Offset), SeasonCalendar.EndOf(Fall2026));
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 23, 59, 59, SeasonCalendar.Offset), SeasonCalendar.EndOf(Summer2026));
    }

    [Fact]
    public void TheNextQuarterWrapsTheYear()
    {
        Assert.Equal(Fall2026, SeasonCalendar.Next(Summer2026));
        Assert.Equal(SeasonId.From(2027, 1), SeasonCalendar.Next(Fall2026));
    }

    [Fact]
    public void SeasonsAreNamedTheWayMarchOfMurlocsNamesThem()
    {
        Assert.Equal("Summer 2026", SeasonCalendar.NameOf(Summer2026));
        Assert.Equal("Fall 2026", SeasonCalendar.NameOf(Fall2026));
        Assert.Equal("Winter 2027", SeasonCalendar.NameOf(SeasonId.From(2027, 1)));
        Assert.Equal("Spring 2027", SeasonCalendar.NameOf(SeasonId.From(2027, 2)));
    }

    [Fact]
    public void TheGraceWeekEndsSevenDaysAfterTheBoundaryToTheSecond()
    {
        var boundary = SeasonCalendar.EndOf(Summer2026);

        Assert.False(SeasonCalendar.IsPastGrace(Summer2026, boundary.AddDays(7).AddSeconds(-1)));
        Assert.True(SeasonCalendar.IsPastGrace(Summer2026, boundary.AddDays(7)));
    }

    [Fact]
    public void TheFirstSeasonIsSummer2026()
    {
        Assert.Equal(Summer2026, SeasonCalendar.First);
    }
}
