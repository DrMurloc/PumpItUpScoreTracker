using System;
using System.Linq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The four numbers (docs/design/march-of-murlocs.md §11.6) and the clock, on 김재현's real
///     Winter 2025 session: 39 charts, 59,319 points, 24.22 balanced against 23.67 by folder,
///     an AAA average, 22:04 of downtime.
/// </summary>
public sealed class MoMLeverMathTests
{
    [Fact]
    public void TheFourNumbersOfTheWinter2025Session()
    {
        var levers = MoMLeverMath.Levers(MoMRealSessions.Winter2025(), MoMRealSessions.Window, MixEnum.Phoenix);

        Assert.Equal(39, levers.ChartsPlayed);
        Assert.Equal(MoMRealSessions.Winter2025Total, levers.TotalScore);
        Assert.Equal(24.22, levers.AverageBalancedLevel, 2);
        Assert.Equal(23.67, levers.AverageFolderLevel, 2);
        Assert.Equal(951755, (int)levers.AverageScore);
        Assert.Equal(PhoenixLetterGrade.AAA, levers.AverageGrade);
        Assert.Equal(TimeSpan.FromSeconds(4976), levers.SongTime);
        Assert.Equal(TimeSpan.FromSeconds(1324), levers.Downtime);
        Assert.Equal(1521, levers.PointsPerChart);
    }

    [Fact]
    public void TheBalancedAverageSitsAboveTheFolderAverageByDesign()
    {
        // A chart with no override sits at nominal + 0.5, so the balanced figure reads about
        // half a level above the folder number — the page labels both so it never reads as a bug.
        var levers = MoMLeverMath.Levers(MoMRealSessions.Winter2025(), MoMRealSessions.Window, MixEnum.Phoenix);
        Assert.True(levers.AverageBalancedLevel > levers.AverageFolderLevel + 0.5);
    }

    [Fact]
    public void ASessionThatOverhangsTheWindowHasNoDowntime()
    {
        var charts = new[]
        {
            new MoMSessionChart(MoMRealSessions.Chart("Long A", 23, 3600), 960000, PhoenixPlate.RoughGame, false, 100, 0, 23.5, null),
            new MoMSessionChart(MoMRealSessions.Chart("Long B", 23, 3000), 960000, PhoenixPlate.RoughGame, false, 100, 0, 23.5, null)
        };
        var levers = MoMLeverMath.Levers(charts, MoMRealSessions.Window, MixEnum.Phoenix);
        Assert.Equal(TimeSpan.Zero, levers.Downtime);
    }

    [Fact]
    public void AnEmptySessionIsAllDowntime()
    {
        var levers = MoMLeverMath.Levers(Array.Empty<MoMSessionChart>(), MoMRealSessions.Window, MixEnum.Phoenix);
        Assert.Equal(0, levers.ChartsPlayed);
        Assert.Equal(MoMRealSessions.Window, levers.Downtime);
        Assert.Equal(0, levers.PointsPerChart);
    }

    [Fact]
    public void TheGradeFollowsTheBoardsMix()
    {
        // 951,755 is AAA on Phoenix and, with Phoenix 2's re-cut floors, a different rung —
        // the grade is read on the mix the board runs, never on Phoenix by default.
        var charts = MoMRealSessions.Winter2025();
        var phoenix = MoMLeverMath.Levers(charts, MoMRealSessions.Window, MixEnum.Phoenix);
        var phoenix2 = MoMLeverMath.Levers(charts, MoMRealSessions.Window, MixEnum.Phoenix2);
        Assert.Equal(phoenix.AverageScore.LetterGradeFor(MixEnum.Phoenix), phoenix.AverageGrade);
        Assert.Equal(phoenix2.AverageScore.LetterGradeFor(MixEnum.Phoenix2), phoenix2.AverageGrade);
    }

    [Fact]
    public void AHandEnteredSessionSpreadsItsRestEvenlyAndFillsTheWindow()
    {
        var timeline = MoMLeverMath.Timeline(MoMRealSessions.Winter2025(), MoMRealSessions.Window);

        Assert.Equal(39, timeline.Count);
        Assert.Equal(TimeSpan.Zero, timeline[0].StartsAt);
        // 38 equal gaps carry the 22:04 of downtime, so the closing chart starts at 1:38:42
        // and ends exactly when the window does.
        var closing = timeline[^1];
        Assert.Equal("Gargoyle - FULL SONG -", closing.Chart.Chart.Song.Name.ToString());
        Assert.Equal(5922, closing.StartsAt.TotalSeconds, 0);
        Assert.Equal(MoMRealSessions.Window.TotalSeconds, (closing.StartsAt + closing.Length).TotalSeconds, 0);
        Assert.True(timeline.Zip(timeline.Skip(1)).All(pair => pair.Second.StartsAt > pair.First.StartsAt));
    }

    [Fact]
    public void PointsPerSecondIsTheChartsPointsOverItsLength()
    {
        var timeline = MoMLeverMath.Timeline(MoMRealSessions.Winter2025(), MoMRealSessions.Window);
        var full = timeline.Single(t => t.Chart.Chart.Song.Name == "8 6 - FULL SONG -");
        Assert.Equal(2990 / 266.0, full.PointsPerSecond, 3);
    }

    [Fact]
    public void AnImportedSessionStartsEachChartWhereItsStampSays()
    {
        // Stamps are recorded at the end of a play; the first chart's start is the zero.
        var t0 = new DateTimeOffset(2026, 8, 8, 5, 20, 0, TimeSpan.Zero);
        var a = MoMRealSessions.Chart("A", 22, 120);
        var b = MoMRealSessions.Chart("B", 22, 100);
        var c = MoMRealSessions.Chart("C", 22, 130);
        var charts = new[]
        {
            new MoMSessionChart(a, 960000, PhoenixPlate.RoughGame, false, 1000, 0, 22.5, t0 + a.Song.Duration),
            new MoMSessionChart(b, 960000, PhoenixPlate.RoughGame, false, 1000, 0, 22.5, t0 + TimeSpan.FromMinutes(5) + b.Song.Duration),
            new MoMSessionChart(c, 960000, PhoenixPlate.RoughGame, false, 1000, 0, 22.5, t0 + TimeSpan.FromMinutes(9) + c.Song.Duration)
        };

        var timeline = MoMLeverMath.Timeline(charts, MoMRealSessions.Window);

        Assert.Equal(TimeSpan.Zero, timeline[0].StartsAt);
        Assert.Equal(TimeSpan.FromMinutes(5), timeline[1].StartsAt);
        Assert.Equal(TimeSpan.FromMinutes(9), timeline[2].StartsAt);
    }

    [Fact]
    public void OneStampMissingMeansTheWholeTimelineIsDerived()
    {
        var a = MoMRealSessions.Chart("A", 22, 120);
        var b = MoMRealSessions.Chart("B", 22, 120);
        var charts = new[]
        {
            new MoMSessionChart(a, 960000, PhoenixPlate.RoughGame, false, 1000, 0, 22.5, DateTimeOffset.UnixEpoch),
            new MoMSessionChart(b, 960000, PhoenixPlate.RoughGame, false, 1000, 0, 22.5, null)
        };
        var timeline = MoMLeverMath.Timeline(charts, TimeSpan.FromMinutes(10));
        // Two charts of two minutes in a ten-minute window: six minutes of rest, all between them.
        Assert.Equal(TimeSpan.FromMinutes(8), timeline[1].StartsAt);
    }
}
