using System;
using System.Linq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The share image's rows (D25): twenty-minute sections of the session by start time, on
///     김재현's real Winter 2025 timeline — 39 charts, rest spread evenly, the closing chart
///     starting at 1:38:42.
/// </summary>
public sealed class MoMSectionsTests
{
    [Fact]
    public void TheRealSessionFallsIntoFiveSectionsWithEveryChartPlacedOnce()
    {
        var timeline = MoMLeverMath.Timeline(MoMRealSessions.Winter2025(), MoMRealSessions.Window);

        var sections = MoMSections.Group(timeline);

        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, sections.Select(s => s.Index));
        Assert.Equal(39, sections.Sum(s => s.Charts.Count));
        Assert.Equal((0, 20), (sections[0].FromMinute, sections[0].ToMinute));
        Assert.Equal((80, 100), (sections[4].FromMinute, sections[4].ToMinute));
        // Gargoyle starts at 98:42, so it closes the 80–100 row; nothing started after 1:40.
        Assert.Equal("Gargoyle - FULL SONG -", sections[4].Charts[^1].Chart.Chart.Song.Name.ToString());
        Assert.All(sections, s => Assert.True(s.Charts.Zip(s.Charts.Skip(1)).All(p => p.Second.StartsAt >= p.First.StartsAt)));
        Assert.All(sections.SelectMany(s => s.Charts.Select(c => (s.Index, c))),
            x => Assert.Equal(x.Index, MoMSections.Index(x.c.StartsAt)));
    }

    [Fact]
    public void AChartStartingAfterHundredMinutesIsTheOpenEndedLastRow()
    {
        Assert.Equal(5, MoMSections.Index(TimeSpan.FromMinutes(100)));
        Assert.Equal(5, MoMSections.Index(TimeSpan.FromMinutes(105)));
        Assert.Equal(4, MoMSections.Index(TimeSpan.FromMinutes(99.9)));
        Assert.Equal(0, MoMSections.Index(TimeSpan.Zero));
        var chart = MoMRealSessions.Chart("Late", 23, 100);
        var late = new MoMTimedChart(new MoMSessionChart(chart, 960000, SharedKernel.Enums.PhoenixPlate.RoughGame, false, 1000, 0, 23.5, null),
            TimeSpan.FromMinutes(101), TimeSpan.FromSeconds(100), 10);
        var section = Assert.Single(MoMSections.Group(new[] { late }));
        Assert.Equal(100, section.FromMinute);
        Assert.Null(section.ToMinute);
    }
}
