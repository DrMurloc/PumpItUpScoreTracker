using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public class SegmentPaceClassifierTests
{
    /// <summary>Segments of uniform row cadence: one per rate, ten seconds each, back to back.</summary>
    private static EnrichedStepChart Chart(int? meter, string? stepsType, params decimal[] ratesPerSecond)
    {
        var rows = new List<EnrichedRow>();
        var segments = new List<SnapshotSegment>();
        decimal start = 0;
        foreach (var rate in ratesPerSecond)
        {
            var end = start + 10;
            if (rate > 0)
                for (var t = start; t < end; t += 1 / rate)
                    rows.Add(new EnrichedRow(t));
            segments.Add(new SnapshotSegment(start, end, null));
            start = end;
        }

        return new EnrichedStepChart(5, false, rows, Array.Empty<SnapshotHold>(),
            Array.Empty<decimal>(), segments, Array.Empty<SnapshotRange>(),
            new Dictionary<MixEnum, StepChartVerdict>(), rows.Count, 0,
            StepsType: stepsType, Meter: meter);
    }

    [Fact]
    public void StampsPaceByBurstRateAgainstTheFoldersOwnDistribution()
    {
        // One folder, twelve uniform segments whose rates all have exact decimal row gaps
        // (so the burst computes the rate exactly): sorted [1,2,4,5,8,10,16,20,25,32,40,50]
        // puts the nearest-rank cutoffs at P10=2, P25=4, P75=25, P90=40. Strictly above P90
        // is Very Fast, above P75 Fast, strictly below P10 Very Slow, below P25 Slow — a
        // value ON a cutoff stays wordless.
        var charts = new Dictionary<Guid, EnrichedStepChart>
        {
            [Guid.NewGuid()] = Chart(20, "pump-single", 1, 2, 4, 5, 8, 10),
            [Guid.NewGuid()] = Chart(20, "pump-single", 16, 20, 25, 32, 40, 50)
        };

        var stamped = SegmentPaceClassifier.Stamp(charts);
        var slowChart = stamped.Values.Single(c => c.Rows.Count < 400);
        var fastChart = stamped.Values.Single(c => c.Rows.Count >= 400);

        Assert.Equal(SegmentPaceClassifier.VerySlow, slowChart.Segments[0].Pace);
        Assert.Equal(SegmentPaceClassifier.Slow, slowChart.Segments[1].Pace);
        Assert.Null(slowChart.Segments[2].Pace);
        Assert.Null(fastChart.Segments[2].Pace);
        Assert.Equal(SegmentPaceClassifier.Fast, fastChart.Segments[3].Pace);
        Assert.Equal(SegmentPaceClassifier.Fast, fastChart.Segments[4].Pace);
        Assert.Equal(SegmentPaceClassifier.VeryFast, fastChart.Segments[5].Pace);
    }

    [Fact]
    public void AShortBurstOutranksALongUnbrokenRun()
    {
        // The field case (Horang's runs vs Solve My Hurt's drills): a segment holding one
        // eight-row 16 rows/s drill in ten otherwise-empty seconds must outrank uniform
        // 10.7 rows/s marathon segments — throughput said the opposite.
        var marathon = Chart(21, "pump-single",
            10.7m, 10.7m, 10.7m, 10.7m, 10.7m, 10.7m, 10.7m, 10.7m, 10.7m, 10.7m, 10.7m);

        var burstRows = Enumerable.Range(0, 8).Select(i => new EnrichedRow(100 + i * 0.0625m)).ToList();
        var burstChart = new EnrichedStepChart(5, false, burstRows, Array.Empty<SnapshotHold>(),
            Array.Empty<decimal>(), new[] { new SnapshotSegment(100, 110, null) },
            Array.Empty<SnapshotRange>(), new Dictionary<MixEnum, StepChartVerdict>(), 8, 0,
            StepsType: "pump-single", Meter: 21);

        var stamped = SegmentPaceClassifier.Stamp(new Dictionary<Guid, EnrichedStepChart>
        {
            [Guid.NewGuid()] = marathon,
            [Guid.NewGuid()] = burstChart
        });

        var drills = stamped.Values.Single(c => c.Segments.Count == 1);
        var runs = stamped.Values.Single(c => c.Segments.Count > 1);
        Assert.Equal(SegmentPaceClassifier.VeryFast, drills.Segments[0].Pace);
        Assert.All(runs.Segments, s => Assert.NotEqual(SegmentPaceClassifier.VeryFast, s.Pace));
    }

    [Fact]
    public void FoldersJudgeIndependently()
    {
        // 8 rows/s is the fastest thing in the slow folder and the slowest in the fast one.
        var slowFolder = Guid.NewGuid();
        var fastFolder = Guid.NewGuid();
        var charts = new Dictionary<Guid, EnrichedStepChart>
        {
            [slowFolder] = Chart(10, "pump-single", 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 7, 8),
            [fastFolder] = Chart(24, "pump-single", 8, 9, 10, 10, 11, 11, 12, 12, 13, 13, 14, 15)
        };

        var stamped = SegmentPaceClassifier.Stamp(charts);

        Assert.Equal(SegmentPaceClassifier.VeryFast, stamped[slowFolder].Segments[^1].Pace);
        Assert.Equal(SegmentPaceClassifier.VerySlow, stamped[fastFolder].Segments[0].Pace);
    }

    [Fact]
    public void ATinyFolderStampsNothing()
    {
        // A chart alone in its folder would only ever be fast relative to itself.
        var charts = new Dictionary<Guid, EnrichedStepChart>
        {
            [Guid.NewGuid()] = Chart(26, "pump-double", 3, 9, 12)
        };

        var stamped = SegmentPaceClassifier.Stamp(charts);

        Assert.All(stamped.Values.Single().Segments, s => Assert.Null(s.Pace));
    }

    [Fact]
    public void ChartsWithoutAFolderPassThroughUntouched()
    {
        var charts = new Dictionary<Guid, EnrichedStepChart>
        {
            [Guid.NewGuid()] = Chart(null, null, 5, 6, 7)
        };

        var stamped = SegmentPaceClassifier.Stamp(charts);

        Assert.All(stamped.Values.Single().Segments, s => Assert.Null(s.Pace));
    }

    [Fact]
    public void BurstMeasuresWindowsAndSparseSpansHonestly()
    {
        // Eight rows at 16/s: a full window, (8-1)/0.4375s = 16. Three rows over two
        // seconds: too short for a window, whole-span rate (3-1)/2s = 1. One row: nothing.
        var burst8 = Enumerable.Range(0, 8).Select(i => i * 0.0625m).ToArray();
        Assert.Equal(16m, SegmentPaceClassifier.Burst(burst8, 0, 10));

        Assert.Equal(1m, SegmentPaceClassifier.Burst(new[] { 0m, 1m, 2m }, 0, 10));
        Assert.Null(SegmentPaceClassifier.Burst(new[] { 5m }, 0, 10));
        Assert.Null(SegmentPaceClassifier.Burst(Array.Empty<decimal>(), 0, 10));

        // Rows outside the segment span never count.
        Assert.Null(SegmentPaceClassifier.Burst(new[] { 20m, 21m }, 0, 10));
    }
}
