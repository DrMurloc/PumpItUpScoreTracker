using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class WeeklyTournamentSagaStaticsTests
{
    [Fact]
    public void ProcessIntoPlacesReturnsEmptyForNoEntries()
    {
        var result = WeeklyTournamentSaga.ProcessIntoPlaces(Array.Empty<WeeklyTournamentEntry>());
        Assert.Empty(result);
    }

    [Fact]
    public void ProcessIntoPlacesAssignsFirstPlaceToTopScore()
    {
        var chartId = Guid.NewGuid();
        var entries = new[]
        {
            Entry(chartId, score: 800_000),
            Entry(chartId, score: 950_000),
            Entry(chartId, score: 900_000)
        };

        var result = WeeklyTournamentSaga.ProcessIntoPlaces(entries).ToArray();
        var first = result.Single(r => r.Item2.Score == 950_000);

        Assert.Equal(1, first.Item1);
    }

    [Fact]
    public void ProcessIntoPlacesAssignsConsecutivePlacesToDistinctScores()
    {
        var chartId = Guid.NewGuid();
        var entries = new[]
        {
            Entry(chartId, score: 800_000),
            Entry(chartId, score: 950_000),
            Entry(chartId, score: 900_000)
        };

        var result = WeeklyTournamentSaga.ProcessIntoPlaces(entries)
            .OrderBy(r => r.Item1)
            .ToArray();

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(r => r.Item1).ToArray());
    }

    [Fact]
    public void ProcessIntoPlacesGivesTiedScoresTheSamePlace()
    {
        var chartId = Guid.NewGuid();
        var entries = new[]
        {
            Entry(chartId, score: 950_000),
            Entry(chartId, score: 950_000),
            Entry(chartId, score: 800_000)
        };

        var result = WeeklyTournamentSaga.ProcessIntoPlaces(entries).ToArray();

        var topPlaces = result.Where(r => r.Item2.Score == 950_000).Select(r => r.Item1).ToArray();
        var bottomPlace = result.Single(r => r.Item2.Score == 800_000).Item1;

        Assert.Equal(new[] { 1, 1 }, topPlaces);
        Assert.Equal(3, bottomPlace);
    }

    [Fact]
    public void GetSuggestedChartsAlwaysIncludesCoOp()
    {
        var coOp = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var result = WeeklyTournamentSaga.GetSuggestedCharts(new[] { coOp }, doublesCompetitive: 0, singlesCompetitive: 0);
        Assert.Contains(coOp, result);
    }

    [Fact]
    public void GetSuggestedChartsIncludesSinglesWithinRangeOfSinglesCompetitive()
    {
        var inRange = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var farTooHigh = new ChartBuilder().WithType(ChartType.Single).WithLevel(25).Build();
        var farTooLow = new ChartBuilder().WithType(ChartType.Single).WithLevel(10).Build();

        var result = WeeklyTournamentSaga
            .GetSuggestedCharts(new[] { inRange, farTooHigh, farTooLow },
                doublesCompetitive: 0, singlesCompetitive: 21)
            .ToHashSet();

        Assert.Contains(inRange, result);
        Assert.DoesNotContain(farTooHigh, result);
        Assert.DoesNotContain(farTooLow, result);
    }

    [Fact]
    public void GetSuggestedChartsIncludesDoublesWithinRangeOfDoublesCompetitive()
    {
        var inRange = new ChartBuilder().WithType(ChartType.Double).WithLevel(18).Build();
        var farTooHigh = new ChartBuilder().WithType(ChartType.Double).WithLevel(25).Build();

        var result = WeeklyTournamentSaga
            .GetSuggestedCharts(new[] { inRange, farTooHigh },
                doublesCompetitive: 19, singlesCompetitive: 0)
            .ToHashSet();

        Assert.Contains(inRange, result);
        Assert.DoesNotContain(farTooHigh, result);
    }

    [Fact]
    public void GetSuggestedChartsExcludesSinglesOutsideSinglesCompetitiveRange()
    {
        var farFromSingles = new ChartBuilder().WithType(ChartType.Single).WithLevel(25).Build();

        var result = WeeklyTournamentSaga
            .GetSuggestedCharts(new[] { farFromSingles },
                doublesCompetitive: 25, singlesCompetitive: 10)
            .ToArray();

        Assert.DoesNotContain(farFromSingles, result);
    }

    private static WeeklyTournamentEntry Entry(Guid chartId, int score) =>
        new(UserId: Guid.NewGuid(), ChartId: chartId, Score: score, Plate: PhoenixPlate.MarvelousGame,
            IsBroken: false, PhotoUrl: null, CompetitiveLevel: 20);
}
