using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Unit tests for the pure per-level aggregator (decision A: the math is tested at the
///     lowest level that catches the defect — no rendering, no I/O). Grade scale here is a
///     toy 8-grade ladder; plate scale is the real 6-plate ladder, both worst → best.
/// </summary>
public sealed class ByLevelAggregatorTests
{
    private static readonly BreakdownScales Scales = new(
        new[] { "F", "D", "C", "B", "A", "S", "SS", "SSS" }, // ranks 0..7
        new[] { "RG", "FG", "TG", "MG", "UG", "PG" }); // ranks 0..5

    private static BreakdownRecord Passed(ChartType type, int bucket, int score, int gradeRank, int plateRank = 5) =>
        new(type, bucket, true, true, score, gradeRank, plateRank);

    private static BreakdownRecord Unplayed(ChartType type, int bucket) =>
        new(type, bucket, false, false, null, null, null);

    private static BreakdownRecord Failed(ChartType type, int bucket) =>
        new(type, bucket, true, false, null, null, null);

    // ---- stat helpers ----

    [Fact]
    public void PercentileLinearlyInterpolates()
    {
        var data = new double[] { 10, 20, 30, 40 };
        Assert.Equal(25, ByLevelAggregator.Percentile(data, 50));
        Assert.Equal(17.5, ByLevelAggregator.Percentile(data, 25));
        Assert.Equal(10, ByLevelAggregator.Percentile(data, 0));
        Assert.Equal(40, ByLevelAggregator.Percentile(data, 100));
    }

    [Fact]
    public void StdDevIsPopulationStandardDeviation()
    {
        var data = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        Assert.Equal(2, ByLevelAggregator.StdDev(data));
    }

    // ---- Distribution ----

    [Fact]
    public void DistributionSeparatesSinglesAndDoublesByLineStyle()
    {
        var records = new[]
        {
            Passed(ChartType.Single, 20, 900_000, 5),
            Passed(ChartType.Single, 20, 1_000_000, 7),
            Passed(ChartType.Double, 20, 800_000, 4),
            Passed(ChartType.Double, 20, 900_000, 5)
        };
        var config = new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.Score,
            Aggregation = BreakdownAggregation.Distribution,
            Series = new() { DistributionSeries.Median },
            SeparateSinglesDoubles = true,
            MinLevel = 20, MaxLevel = 20
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        Assert.Equal(BreakdownChartKind.Lines, result.Kind);
        Assert.Equal(2, result.Series.Count);
        var singles = Assert.Single(result.Series, s => s.Type == ChartType.Single);
        var doubles = Assert.Single(result.Series, s => s.Type == ChartType.Double);
        Assert.False(singles.Dashed);
        Assert.True(doubles.Dashed); // Doubles = dashed, never a second hue
        Assert.Equal(950_000, singles.Values[0]); // median of 900k / 1M
        Assert.Equal(850_000, doubles.Values[0]); // median of 800k / 900k
    }

    [Fact]
    public void DistributionCountsClearedChartsOnlyNotFails()
    {
        var records = new[]
        {
            Passed(ChartType.Single, 20, 950_000, 5),
            Passed(ChartType.Single, 20, 1_000_000, 7),
            Failed(ChartType.Single, 20) // a failed attempt must not drag the distribution down
        };
        var config = new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.Score,
            Aggregation = BreakdownAggregation.Distribution,
            Series = new() { DistributionSeries.Min },
            SeparateSinglesDoubles = false,
            MinLevel = 20, MaxLevel = 20
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        Assert.Equal(950_000, Assert.Single(result.Series).Values[0]); // not 0 / the fail
    }

    [Fact]
    public void DistributionEmitsBandWhenRequested()
    {
        var records = new[]
        {
            Passed(ChartType.Single, 20, 900_000, 5),
            Passed(ChartType.Single, 20, 950_000, 5),
            Passed(ChartType.Single, 20, 1_000_000, 7)
        };
        var config = new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.Score,
            Aggregation = BreakdownAggregation.Distribution,
            Series = new() { DistributionSeries.Median },
            Band = BreakdownBand.MinMax,
            SeparateSinglesDoubles = false,
            MinLevel = 20, MaxLevel = 20
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        var band = Assert.Single(result.Bands);
        Assert.Equal(900_000, band.Lower[0]);
        Assert.Equal(1_000_000, band.Upper[0]);
    }

    // ---- Completion ----

    [Fact]
    public void CompletionCountsOverTheWholeFolderUnplayedIsNotMet()
    {
        var records = new[]
        {
            Passed(ChartType.Single, 20, 1_000_000, 7),
            Passed(ChartType.Single, 20, 990_000, 6),
            Passed(ChartType.Single, 20, 950_000, 5), // below threshold
            Unplayed(ChartType.Single, 20) // counts against completion
        };
        var config = new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.Score,
            Aggregation = BreakdownAggregation.Completion,
            Thresholds = new() { new CompletionThreshold { Kind = ThresholdKind.Score, Value = "990000" } },
            SeparateSinglesDoubles = false,
            MinLevel = 20, MaxLevel = 20
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        Assert.Equal(50, Assert.Single(result.Series).Values[0]); // 2 of 4, not 2 of 3
        Assert.Equal(0, result.SuggestedYMin);
        Assert.Equal(100, result.SuggestedYMax);
    }

    [Fact]
    public void CompletionSupportsMultipleThresholdsAsMultipleLines()
    {
        var records = new[]
        {
            Passed(ChartType.Single, 20, 1_000_000, 7),
            Passed(ChartType.Single, 20, 990_000, 6),
            Passed(ChartType.Single, 20, 920_000, 4),
            Unplayed(ChartType.Single, 20)
        };
        var config = new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.Score,
            Aggregation = BreakdownAggregation.Completion,
            Thresholds = new()
            {
                new CompletionThreshold { Kind = ThresholdKind.Score, Value = "900000" },
                new CompletionThreshold { Kind = ThresholdKind.Score, Value = "990000" }
            },
            SeparateSinglesDoubles = false,
            MinLevel = 20, MaxLevel = 20
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        Assert.Equal(2, result.Series.Count);
        Assert.Equal(75, result.Series[0].Values[0]); // >= 900k : 3 of 4
        Assert.Equal(50, result.Series[1].Values[0]); // >= 990k : 2 of 4
    }

    [Fact]
    public void CompletionPassIsShareOfFolderCleared()
    {
        var records = new[]
        {
            Passed(ChartType.Single, 20, 950_000, 5),
            Passed(ChartType.Single, 20, 960_000, 5),
            Passed(ChartType.Single, 20, 970_000, 5),
            Failed(ChartType.Single, 20)
        };
        var config = new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.Pass,
            Aggregation = BreakdownAggregation.Completion,
            SeparateSinglesDoubles = false,
            MinLevel = 20, MaxLevel = 20
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        Assert.Equal(75, Assert.Single(result.Series).Values[0]); // 3 cleared of 4
    }

    // ---- Breakdown ----

    [Fact]
    public void BreakdownCountsGradesAndTheUnplayedRemainder()
    {
        var records = new[]
        {
            Passed(ChartType.Single, 20, 1_000_000, 7), // SSS
            Passed(ChartType.Single, 20, 970_000, 5), // S
            Passed(ChartType.Single, 20, 972_000, 5), // S
            Unplayed(ChartType.Single, 20),
            Unplayed(ChartType.Single, 20)
        };
        var config = new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.LetterGrade,
            Aggregation = BreakdownAggregation.Breakdown,
            Normalize = false,
            IncludeUnplayed = true,
            SeparateSinglesDoubles = false,
            MinLevel = 20, MaxLevel = 20
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        Assert.Equal(BreakdownChartKind.StackedBars, result.Kind);
        Assert.Equal(2, Value(result, "Not cleared"));
        Assert.Equal(2, Value(result, "S"));
        Assert.Equal(1, Value(result, "SSS"));
        Assert.Equal(0, Value(result, "F"));
    }

    [Fact]
    public void BreakdownNormalizesToFolderPercentWhenAsked()
    {
        var records = new[]
        {
            Passed(ChartType.Single, 20, 1_000_000, 7),
            Passed(ChartType.Single, 20, 970_000, 5),
            Passed(ChartType.Single, 20, 972_000, 5),
            Unplayed(ChartType.Single, 20),
            Unplayed(ChartType.Single, 20)
        };
        var config = new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.LetterGrade,
            Aggregation = BreakdownAggregation.Breakdown,
            Normalize = true,
            IncludeUnplayed = true,
            SeparateSinglesDoubles = false,
            MinLevel = 20, MaxLevel = 20
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        Assert.Equal(40, Value(result, "Not cleared")); // 2 of 5
        Assert.Equal(40, Value(result, "S")); // 2 of 5
        Assert.Equal(20, Value(result, "SSS")); // 1 of 5
    }

    [Fact]
    public void BreakdownPassSplitsPassedFromUnpassed()
    {
        var records = new[]
        {
            Passed(ChartType.Single, 20, 950_000, 5),
            Passed(ChartType.Single, 20, 960_000, 5),
            Failed(ChartType.Single, 20),
            Unplayed(ChartType.Single, 20)
        };
        var config = new ByLevelBreakdownConfig
        {
            Metric = BreakdownMetric.Pass,
            Aggregation = BreakdownAggregation.Breakdown,
            SeparateSinglesDoubles = false,
            MinLevel = 20, MaxLevel = 20
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        Assert.Equal(2, Value(result, "Passed"));
        Assert.Equal(2, Value(result, "Unpassed")); // 1 failed + 1 unplayed
    }

    // ---- scope ----

    [Fact]
    public void CoOpBucketsArePlayerCountsOnAPlayersAxis()
    {
        var records = new[]
        {
            new BreakdownRecord(ChartType.CoOp, 2, true, true, 900_000, 5, 5),
            new BreakdownRecord(ChartType.CoOp, 3, true, true, 800_000, 4, 4),
            new BreakdownRecord(ChartType.Single, 20, true, true, 900_000, 5, 5) // ignored in co-op scope
        };
        var config = new ByLevelBreakdownConfig
        {
            Scope = BreakdownChartScope.CoOp,
            Metric = BreakdownMetric.Pass,
            Aggregation = BreakdownAggregation.Completion,
            MinPlayers = 2, MaxPlayers = 3
        };

        var result = ByLevelAggregator.Aggregate(config, records, Scales);

        Assert.Equal(new[] { 2, 3 }, result.Buckets);
        Assert.Equal("Players", result.XAxisTitle);
    }

    [Fact]
    public void NoRecordsYieldsEmpty()
    {
        var result = ByLevelAggregator.Aggregate(new ByLevelBreakdownConfig(), Array.Empty<BreakdownRecord>(), Scales);
        Assert.Equal(BreakdownChartKind.Empty, result.Kind);
    }

    private static double? Value(BreakdownResult result, string label) =>
        result.Series.Single(s => s.Label == label).Values[0];
}
