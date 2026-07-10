using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Domain.Recap;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class WeeklyRecapCalculatorTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Chart ChartA = new ChartBuilder().WithType(ChartType.Double).WithLevel(22)
        .WithSongName("District 1").Build();
    private static readonly DateTimeOffset Week1 = new(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);

    private static WeeklyPlacingRow Row(Guid userId, DateTimeOffset week, int score,
        bool withinRange = true, double level = 20.0, Guid? chartId = null)
    {
        return new WeeklyPlacingRow(userId, chartId ?? ChartA.Id, week, 0, score, false, withinRange, level);
    }

    private static IReadOnlyDictionary<Guid, Chart> Charts => new Dictionary<Guid, Chart> { [ChartA.Id] = ChartA };

    private static IReadOnlyDictionary<Guid, string> Names(params (Guid Id, string Name)[] names)
    {
        return names.ToDictionary(n => n.Id, n => n.Name);
    }

    [Fact]
    public void PlayersWithNoWeeklyHistoryGetNoSection()
    {
        var rows = new[] { Row(Guid.NewGuid(), Week1, 950_000) };

        Assert.Null(WeeklyRecapCalculator.Calculate(Me, rows, Charts, Names()));
    }

    [Fact]
    public void StreaksCountConsecutiveRotationsNotCalendarWeeks()
    {
        // The board skipped a rotation between weeks 2 and 4 — nobody has rows there, so
        // playing weeks 1, 2, and 4 is an unbroken three-rotation streak.
        var someoneElse = Guid.NewGuid();
        var rows = new[]
        {
            Row(Me, Week1, 900_000), Row(Me, Week1.AddDays(7), 900_000), Row(Me, Week1.AddDays(21), 900_000),
            Row(someoneElse, Week1, 910_000), Row(someoneElse, Week1.AddDays(7), 910_000),
            Row(someoneElse, Week1.AddDays(21), 910_000)
        };

        var weekly = WeeklyRecapCalculator.Calculate(Me, rows, Charts, Names());

        Assert.Equal(3, weekly!.LongestStreak);
        Assert.Equal(3, weekly.WeeksEntered);
    }

    [Fact]
    public void PlacementsReRankAmongWithinRangeEntrantsOnly()
    {
        var inRangeBetter = Guid.NewGuid();
        var outOfRangeTourist = Guid.NewGuid();
        var rows = new[]
        {
            Row(Me, Week1, 950_000),
            Row(inRangeBetter, Week1, 960_000),
            Row(outOfRangeTourist, Week1, 990_000, withinRange: false, level: 26)
        };

        var weekly = WeeklyRecapCalculator.Calculate(Me, rows, Charts, Names());

        Assert.Equal(2, weekly!.BestWeek!.Place);
        Assert.Equal(2, weekly.BestWeek.OfCount);
        Assert.Equal(1, weekly.PodiumsInRange);
        Assert.Equal(0, weekly.WinsInRange);
    }

    [Fact]
    public void GiantSlayerRequiresAFullLevelGapAndAWin()
    {
        var slightlyAbove = Guid.NewGuid();
        var giant = Guid.NewGuid();
        var giantWhoBeatMe = Guid.NewGuid();
        var rows = new[]
        {
            Row(Me, Week1, 950_000, level: 20.0),
            Row(slightlyAbove, Week1, 940_000, level: 20.9),
            Row(giant, Week1, 940_000, level: 21.4),
            Row(giantWhoBeatMe, Week1, 990_000, level: 22.0)
        };

        var weekly = WeeklyRecapCalculator.Calculate(Me, rows, Charts,
            Names((giant, "WIDEBOY #3311")));

        Assert.Equal(1, weekly!.GiantSlayerCount);
        var moment = Assert.Single(weekly.TopGiantSlayers);
        Assert.Equal("WIDEBOY #3311", moment.OutscoredName);
        Assert.Equal(1.4, moment.LevelGap, 5);
    }

    [Fact]
    public void TopGiantSlayersOrderByGapDescending()
    {
        var small = Guid.NewGuid();
        var medium = Guid.NewGuid();
        var large = Guid.NewGuid();
        var extra = Guid.NewGuid();
        var rows = new[]
        {
            Row(Me, Week1, 980_000, level: 20.0),
            Row(small, Week1, 940_000, level: 21.1),
            Row(medium, Week1, 941_000, level: 21.5),
            Row(large, Week1, 942_000, level: 22.3),
            Row(extra, Week1, 943_000, level: 21.05)
        };

        var weekly = WeeklyRecapCalculator.Calculate(Me, rows, Charts, Names());

        Assert.Equal(4, weekly!.GiantSlayerCount);
        Assert.Equal(new[] { 2.3, 1.5, 1.1 },
            weekly.TopGiantSlayers.Select(g => Math.Round(g.LevelGap, 5)).ToArray());
    }

    [Fact]
    public void TheSameGiantOnlyCountsOnceAcrossWeeks()
    {
        var giant = Guid.NewGuid();
        var rows = new[]
        {
            Row(Me, Week1, 980_000, level: 20.0),
            Row(giant, Week1, 940_000, level: 21.5),
            Row(Me, Week1.AddDays(7), 985_000, level: 20.0),
            Row(giant, Week1.AddDays(7), 950_000, level: 21.5),
            Row(Me, Week1.AddDays(14), 990_000, level: 20.0),
            Row(giant, Week1.AddDays(14), 960_000, level: 21.5)
        };

        var weekly = WeeklyRecapCalculator.Calculate(Me, rows, Charts, Names());

        Assert.Equal(1, weekly!.GiantSlayerCount);
        Assert.Single(weekly.TopGiantSlayers);
    }

    [Fact]
    public void BrokenRunsOnEitherSideAreNotSlayings()
    {
        var brokenGiant = Guid.NewGuid();
        var giantIBrokeAgainst = Guid.NewGuid();
        var week2 = Week1.AddDays(7);
        var rows = new[]
        {
            // Week 1: the giant broke the chart — beating their break is no story.
            Row(Me, Week1, 950_000, level: 20.0),
            new WeeklyPlacingRow(brokenGiant, ChartA.Id, Week1, 0, 900_000, true, false, 21.5),
            // Week 2: I broke — outscoring someone while failing the chart counts even less.
            new WeeklyPlacingRow(Me, ChartA.Id, week2, 0, 960_000, true, true, 20.0),
            Row(giantIBrokeAgainst, week2, 940_000, level: 21.5)
        };

        var weekly = WeeklyRecapCalculator.Calculate(Me, rows, Charts, Names());

        Assert.Equal(0, weekly!.GiantSlayerCount);
    }

    [Fact]
    public void BestWeekPrefersLowerPlaceThenBiggerPool()
    {
        var other = Guid.NewGuid();
        var week2 = Week1.AddDays(7);
        var rows = new[]
        {
            // Week 1: I win a two-player pool.
            Row(Me, Week1, 960_000), Row(other, Week1, 950_000),
            // Week 2: I win alone — same rank, smaller pool, must not displace week 1.
            Row(Me, week2, 940_000)
        };

        var weekly = WeeklyRecapCalculator.Calculate(Me, rows, Charts, Names());

        Assert.Equal(1, weekly!.BestWeek!.Place);
        Assert.Equal(2, weekly.BestWeek.OfCount);
        Assert.Equal(2, weekly.WinsInRange);
    }
}
