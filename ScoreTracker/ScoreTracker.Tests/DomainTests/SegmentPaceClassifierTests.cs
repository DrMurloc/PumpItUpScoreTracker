using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public class SegmentPaceClassifierTests
{
    private static EnrichedStepChart Chart(int? meter, string? stepsType, params decimal?[] enps)
    {
        return new EnrichedStepChart(5, false, Array.Empty<EnrichedRow>(),
            Array.Empty<SnapshotHold>(), Array.Empty<decimal>(),
            enps.Select((value, i) => new SnapshotSegment(i * 10, i * 10 + 10, value)).ToArray(),
            Array.Empty<SnapshotRange>(),
            new Dictionary<MixEnum, StepChartVerdict>(), 0, 0,
            StepsType: stepsType, Meter: meter);
    }

    [Fact]
    public void StampsPaceAgainstTheFoldersOwnDistribution()
    {
        // One folder, twenty segments with eNPS 1..20: P10=2, P25=5, P75=15, P90=18
        // (nearest rank). Strictly above P90 is Very Fast, above P75 Fast, strictly below
        // P10 Very Slow, below P25 Slow — the middle half says nothing.
        var charts = new Dictionary<Guid, EnrichedStepChart>
        {
            [Guid.NewGuid()] = Chart(20, "pump-single", 1, 2, 3, 4, 5, 6, 7, 8, 9, 10),
            [Guid.NewGuid()] = Chart(20, "pump-single", 11, 12, 13, 14, 15, 16, 17, 18, 19, 20)
        };

        var stamped = SegmentPaceClassifier.Stamp(charts).Values
            .SelectMany(c => c.Segments)
            .ToDictionary(s => s.Enps!.Value, s => s.Pace);

        Assert.Equal(SegmentPaceClassifier.VeryFast, stamped[20]);
        Assert.Equal(SegmentPaceClassifier.VeryFast, stamped[19]);
        Assert.Equal(SegmentPaceClassifier.Fast, stamped[18]);
        Assert.Equal(SegmentPaceClassifier.Fast, stamped[16]);
        Assert.Null(stamped[15]);
        Assert.Null(stamped[5]);
        Assert.Equal(SegmentPaceClassifier.Slow, stamped[4]);
        Assert.Equal(SegmentPaceClassifier.Slow, stamped[2]);
        Assert.Equal(SegmentPaceClassifier.VerySlow, stamped[1]);
    }

    [Fact]
    public void FoldersJudgeIndependently()
    {
        // 8 eNPS is the fastest thing in the slow folder and the slowest in the fast one.
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
    public void SegmentsWithoutEnpsStayUnstamped()
    {
        var id = Guid.NewGuid();
        var charts = new Dictionary<Guid, EnrichedStepChart>
        {
            [id] = Chart(20, "pump-single", 1, 2, 3, 4, 5, 6, null, 7, 8, 9, 10, 11, 30)
        };

        var stamped = SegmentPaceClassifier.Stamp(charts);

        Assert.Null(stamped[id].Segments[6].Pace);
        Assert.Equal(SegmentPaceClassifier.VeryFast, stamped[id].Segments[^1].Pace);
    }
}
