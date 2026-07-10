using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.PlayerProgress.Domain.Recap;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class Phoenix2ProjectionCalculatorTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RecordedPhoenixScore Record(Chart chart, int score,
        PhoenixPlate plate = PhoenixPlate.FairGame, bool isBroken = false)
    {
        return new RecordedPhoenixScore(chart.Id, score, plate, isBroken, RecordedAt);
    }

    private static double P2Rating(Chart chart, int score, PhoenixPlate plate = PhoenixPlate.FairGame)
    {
        return ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false)
            .GetScore(chart.Type, chart.Level, score, plate, false);
    }

    [Fact]
    public void NothingCarriesOverMeansNoProjection()
    {
        var record = Record(new ChartBuilder().Build(), 950_000);

        Assert.Null(Phoenix2ProjectionCalculator.Calculate(new[] { record },
            new Dictionary<Guid, Chart>()));
    }

    [Fact]
    public void PoolsSplitSinglesAndDoublesAndSumToTheTotal()
    {
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var dbl = new ChartBuilder().WithType(ChartType.Double).WithLevel(22).Build();
        var p2Charts = new Dictionary<Guid, Chart> { [single.Id] = single, [dbl.Id] = dbl };

        var projection = Phoenix2ProjectionCalculator.Calculate(
            new[] { Record(single, 975_000), Record(dbl, 960_000) }, p2Charts);

        Assert.Equal((int)P2Rating(single, 975_000), projection!.SinglesPumbility);
        Assert.Equal((int)P2Rating(dbl, 960_000), projection.DoublesPumbility);
        Assert.Equal(projection.SinglesPumbility + projection.DoublesPumbility, projection.TotalPumbility);
    }

    [Fact]
    public void ChartsCutFromPhoenix2AreRescoredAtTheirNewLevels()
    {
        // The same chart id carries a different level in Phoenix 2 — the projection must
        // use the P2 chart's level, not the P1 one.
        var chartId = Guid.NewGuid();
        var p2Chart = new ChartBuilder().WithId(chartId).WithType(ChartType.Single).WithLevel(21).Build();
        var cutChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(26).Build();
        var p2Charts = new Dictionary<Guid, Chart> { [chartId] = p2Chart };

        var projection = Phoenix2ProjectionCalculator.Calculate(
            new[] { Record(p2Chart, 980_000), Record(cutChart, 999_000) }, p2Charts);

        Assert.Equal((int)P2Rating(p2Chart, 980_000), projection!.SinglesPumbility);
        Assert.Equal(1, projection.CarriedOverPasses);
        Assert.Equal(2, projection.TotalPasses);
    }

    [Fact]
    public void PoolsCapAtTheTopFifty()
    {
        var charts = Enumerable.Range(0, 55)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build())
            .ToArray();
        var p2Charts = charts.ToDictionary(c => c.Id);
        var records = charts.Select(c => Record(c, 975_000)).ToArray();

        var projection = Phoenix2ProjectionCalculator.Calculate(records, p2Charts);

        Assert.Equal((int)(50 * P2Rating(charts[0], 975_000)), projection!.SinglesPumbility);
    }

    [Fact]
    public void ProjectedTitlesPickTheHighestReachedRung()
    {
        Assert.Equal("[S] INTERMEDIATE LV.1",
            Phoenix2ProjectionCalculator.ProjectedTitle(PumbilityPool.Singles, 5_999));
        Assert.Equal("[S] INTERMEDIATE LV.2",
            Phoenix2ProjectionCalculator.ProjectedTitle(PumbilityPool.Singles, 6_000));
        Assert.Equal("[D] INTERMEDIATE LV.1",
            Phoenix2ProjectionCalculator.ProjectedTitle(PumbilityPool.Doubles, 5_000));
    }
}
