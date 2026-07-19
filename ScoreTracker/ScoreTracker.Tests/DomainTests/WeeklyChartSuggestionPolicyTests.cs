using System;
using System.Linq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class WeeklyChartSuggestionPolicyTests
{
    private static WeeklyTournamentEntry Entry(int score, bool isBroken = false)
    {
        return new WeeklyTournamentEntry(Guid.NewGuid(), Guid.NewGuid(), score, PhoenixPlate.SuperbGame,
            isBroken, null, 20.0);
    }

    [Fact]
    public void ProcessIntoPlacesAscendingRanksLowestPassingFirst()
    {
        var low = Entry(720_000);
        var mid = Entry(850_000);
        var high = Entry(990_000);

        var places = WeeklyChartSuggestionPolicy.ProcessIntoPlacesAscending(new[] { high, low, mid })
            .ToDictionary(p => p.Item2, p => p.Item1);

        Assert.Equal(1, places[low]);
        Assert.Equal(2, places[mid]);
        Assert.Equal(3, places[high]);
    }

    [Fact]
    public void ProcessIntoPlacesAscendingExcludesBrokenRuns()
    {
        var brokenLower = Entry(600_000, isBroken: true);
        var passing = Entry(910_000);

        var result = WeeklyChartSuggestionPolicy.ProcessIntoPlacesAscending(new[] { brokenLower, passing })
            .ToArray();

        Assert.Single(result);
        Assert.Equal((1, passing), result[0]);
    }

    [Fact]
    public void ProcessIntoPlacesAscendingSharesPlaceOnTiesThenJumps()
    {
        var tieA = Entry(800_000);
        var tieB = Entry(800_000);
        var higher = Entry(900_000);

        var places = WeeklyChartSuggestionPolicy.ProcessIntoPlacesAscending(new[] { higher, tieA, tieB })
            .ToArray();

        Assert.Equal(1, places.Single(p => p.Item2 == tieA).Item1);
        Assert.Equal(1, places.Single(p => p.Item2 == tieB).Item1);
        // Two entries tie at place 1, so the next jumps to place 3.
        Assert.Equal(3, places.Single(p => p.Item2 == higher).Item1);
    }

    [Fact]
    public void ProcessIntoPlacesAscendingReturnsEmptyWhenAllBroken()
    {
        var result = WeeklyChartSuggestionPolicy.ProcessIntoPlacesAscending(
            new[] { Entry(500_000, isBroken: true), Entry(700_000, isBroken: true) });

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(19.0, true)] // floor 19 = level − 1: the band's bottom edge
    [InlineData(18.99, false)] // floor 18, one below the bottom edge
    [InlineData(22.9, true)] // floor 22 = level + 2: the band's top edge
    [InlineData(23.0, false)] // floor 23, one above the top edge
    [InlineData(20.0, true)] // dead center
    public void WithinRangeBandIsFloorOfLevelMinusOneToPlusTwo(double competitiveLevel, bool expected)
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();

        Assert.Equal(expected, WeeklyChartSuggestionPolicy.IsWithinRange(chart, competitiveLevel));
    }

    [Fact]
    public void CoOpChartsAreAlwaysWithinRange()
    {
        var coOp = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(2).Build();

        Assert.True(WeeklyChartSuggestionPolicy.IsWithinRange(coOp, 5.0));
    }

    [Fact]
    public void SuggestedChartsUseTheSameBandPerChartType()
    {
        var single18 = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var double18 = new ChartBuilder().WithType(ChartType.Double).WithLevel(18).Build();
        var single24 = new ChartBuilder().WithType(ChartType.Single).WithLevel(24).Build();

        // Singles CL 19 covers a S18; doubles CL 16 leaves a D18 out of reach.
        var suggested = WeeklyChartSuggestionPolicy.GetSuggestedCharts(
            new[] { single18, double18, single24 }, doublesCompetitive: 16.0, singlesCompetitive: 19.0).ToArray();

        Assert.Contains(single18, suggested);
        Assert.DoesNotContain(double18, suggested);
        Assert.DoesNotContain(single24, suggested);
    }
}
