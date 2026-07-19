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
    public void PoolsSplitSinglesAndDoublesAndTotalIsAMergedTop50()
    {
        // Two charts fit inside one merged top-50, so the total here coincides with
        // Singles + Doubles; TotalIsAMergedTop50NotTwoPoolsSummed covers where they diverge.
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var dbl = new ChartBuilder().WithType(ChartType.Double).WithLevel(22).Build();
        var p2Charts = new Dictionary<Guid, Chart> { [single.Id] = single, [dbl.Id] = dbl };

        var projection = Phoenix2ProjectionCalculator.Calculate(
            new[] { Record(single, 975_000), Record(dbl, 960_000) }, p2Charts);

        Assert.Equal((int)P2Rating(single, 975_000), projection!.SinglesPumbility);
        Assert.Equal((int)P2Rating(dbl, 960_000), projection.DoublesPumbility);
        // The merged total floors the EXACT sum — the per-pool ints can land one below it
        // when the two fractional parts add past 1.
        Assert.Equal((int)(P2Rating(single, 975_000) + P2Rating(dbl, 960_000)), projection.TotalPumbility);
    }

    [Fact]
    public void TotalIsAMergedTop50NotTwoPoolsSummed()
    {
        // 30 singles (level 22) outrate 30 doubles (level 20). Each per-type pool holds all
        // 30 of its type, but the merged total keeps only the 50 best of the 60 — the 30
        // singles plus the 20 best doubles — so Total < Singles + Doubles.
        var singles = Enumerable.Range(0, 30)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build()).ToArray();
        var doubles = Enumerable.Range(0, 30)
            .Select(_ => new ChartBuilder().WithType(ChartType.Double).WithLevel(20).Build()).ToArray();
        var all = singles.Concat(doubles).ToArray();
        var p2Charts = all.ToDictionary(c => c.Id);
        var records = all.Select(c => Record(c, 975_000)).ToArray();

        var vs = P2Rating(singles[0], 975_000);
        var vd = P2Rating(doubles[0], 975_000);
        Assert.True(vs > vd, "test setup: singles must outrate doubles so doubles are dropped");

        var projection = Phoenix2ProjectionCalculator.Calculate(records, p2Charts)!;

        // Accumulate exactly as the calculator's LINQ Sum does (order + flooring).
        var expectedTotal = (int)Enumerable.Repeat(vs, 30).Concat(Enumerable.Repeat(vd, 20)).Sum();
        Assert.Equal(expectedTotal, projection.TotalPumbility);
        Assert.True(projection.TotalPumbility < projection.SinglesPumbility + projection.DoublesPumbility);
        Assert.True(projection.TotalPumbility > Math.Max(projection.SinglesPumbility, projection.DoublesPumbility));
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
