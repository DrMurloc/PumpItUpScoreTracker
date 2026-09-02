using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public class StepChartEnricherTests
{
    private static SnapshotStepData Snapshot(
        IReadOnlyList<SnapshotArrow>? taps = null,
        IReadOnlyList<SnapshotHold>? holds = null,
        IReadOnlyList<SnapshotTickSpan>? ticks = null,
        string stepsType = "pump-single")
    {
        return new SnapshotStepData(
            taps ?? new[] { new SnapshotArrow(0, 1.0m, "l") },
            holds ?? Array.Empty<SnapshotHold>(),
            ticks ?? Array.Empty<SnapshotTickSpan>(),
            Array.Empty<SnapshotSegment>(),
            Array.Empty<SnapshotRange>(),
            "pack/song/song.ssc", stepsType, 21);
    }

    private static IReadOnlyDictionary<MixEnum, int?> Counts(int? phoenix = null, int? phoenix2 = null)
    {
        return new Dictionary<MixEnum, int?> { [MixEnum.Phoenix] = phoenix, [MixEnum.Phoenix2] = phoenix2 };
    }

    private static IReadOnlyList<SnapshotArrow> Taps(params decimal[] times)
    {
        return times.Select(t => new SnapshotArrow(0, t, "l")).ToArray();
    }

    [Fact]
    public void MoreTapRowsThanTheGameJudgesIsExcluded()
    {
        var enriched = StepChartEnricher.Enrich(Snapshot(Taps(1m, 2m, 3m)), null, Counts(2));

        Assert.Equal(StepChartVisibility.Excluded, enriched.Verdicts[MixEnum.Phoenix].Visibility);
    }

    [Fact]
    public void FewerDerivedTicksThanHoldRowsIsExcluded()
    {
        // 3 tap rows + 2 hold rows against a judged total of 4: one derived tick for two holds.
        var snapshot = Snapshot(Taps(1m, 2m, 3m),
            new[] { new SnapshotHold(1, 4m, 5m, "r"), new SnapshotHold(2, 6m, 7m, "r") });

        var enriched = StepChartEnricher.Enrich(snapshot, null, Counts(4));

        Assert.Equal(StepChartVisibility.Excluded, enriched.Verdicts[MixEnum.Phoenix].Visibility);
    }

    [Fact]
    public void AHoldlessFileAgainstAHoldyGameIsExcluded()
    {
        // 90 tap rows, no holds, judged 100: the game demands 10% of itself in ticks the file
        // cannot produce — the Final Audition 2 SHORT CUT shape.
        var enriched = StepChartEnricher.Enrich(
            Snapshot(Taps(Enumerable.Range(1, 90).Select(i => (decimal)i).ToArray())), null, Counts(100));

        Assert.Equal(StepChartVisibility.Excluded, enriched.Verdicts[MixEnum.Phoenix].Visibility);
    }

    [Fact]
    public void WildlyDisagreeingTotalsAreExcluded()
    {
        // The Conflict D26 shape: implied miles past judged.
        var snapshot = Snapshot(Taps(1m, 2m, 3m, 4m),
            new[] { new SnapshotHold(1, 5m, 6m, "r") },
            new[] { new SnapshotTickSpan(5m, 6m, 100) });

        var enriched = StepChartEnricher.Enrich(snapshot, null, Counts(20));

        Assert.Equal(StepChartVisibility.Excluded, enriched.Verdicts[MixEnum.Phoenix].Visibility);
    }

    [Fact]
    public void WithinTheGateIsFullAndPastItIsStepsOnly()
    {
        // 98 rows + 1 hold + 1 tick = implied 99 against judged 100: inside 2%. Against 120 the
        // same file is 17% out — no tier fires, but the gate refuses pins: strip yes, pins no.
        var snapshot = Snapshot(Taps(Enumerable.Range(1, 98).Select(i => (decimal)i).ToArray()),
            new[] { new SnapshotHold(1, 99m, 100m, "r") },
            new[] { new SnapshotTickSpan(99m, 100m, 1) });

        var enriched = StepChartEnricher.Enrich(snapshot, null, Counts(100, 120));

        Assert.Equal(StepChartVisibility.Full, enriched.Verdicts[MixEnum.Phoenix].Visibility);
        Assert.Equal(StepChartVisibility.StepsOnly, enriched.Verdicts[MixEnum.Phoenix2].Visibility);
    }

    [Fact]
    public void NoJudgedTotalLicensesTheStripAndNothingMore()
    {
        var enriched = StepChartEnricher.Enrich(Snapshot(), null, Counts());

        Assert.Equal(StepChartVisibility.StepsOnly, enriched.Verdicts[MixEnum.Phoenix].Visibility);
        Assert.Equal(StepChartVisibility.StepsOnly, enriched.Verdicts[MixEnum.Phoenix2].Visibility);
    }

    [Fact]
    public void AJumpIsOneRowAndTheLeftFootRidesItsOwnMask()
    {
        var taps = new[]
        {
            new SnapshotArrow(0, 1.0m, "l"),
            new SnapshotArrow(4, 1.0m, "r"),
            new SnapshotArrow(2, 2.0m, "r")
        };

        var enriched = StepChartEnricher.Enrich(Snapshot(taps), null, Counts(2));

        Assert.Equal(2, enriched.Rows.Count);
        Assert.Equal((1 << 0) | (1 << 4), enriched.Rows[0].PanelMask);
        Assert.Equal(1 << 0, enriched.Rows[0].LeftMask);
        Assert.Equal(2, enriched.TapRowCount);
    }

    [Fact]
    public void PanelsFollowTheStepsTypeAndTheEvidence()
    {
        Assert.Equal(5, StepChartEnricher.Enrich(Snapshot(), null, Counts(1)).Panels);
        Assert.Equal(10, StepChartEnricher.Enrich(Snapshot(stepsType: "pump-double"), null, Counts(1)).Panels);
        // A "single" whose arrows use panel 7 is not a singles chart, whatever the tag says.
        Assert.Equal(10, StepChartEnricher.Enrich(
            Snapshot(new[] { new SnapshotArrow(7, 1.0m, "l") }), null, Counts(1)).Panels);
    }

    [Fact]
    public void AlignedRowsCarryBeatsAndQuantization()
    {
        var snapshot = Snapshot(Taps(0.0m, 0.25m, 0.5m));
        var ssc = new StepChartData(5, true,
            new[]
            {
                new StepRow(0m, 1) { Time = 0.0m },
                new StepRow(0.5m, 1) { Time = 0.25m },
                new StepRow(1m, 1) { Time = 0.5m }
            },
            Array.Empty<StepHold>(), Array.Empty<StepTick>());

        var enriched = StepChartEnricher.Enrich(snapshot, ssc, Counts(3));

        Assert.True(enriched.BeatsAligned);
        Assert.Equal(new decimal?[] { 0m, 0.5m, 1m }, enriched.Rows.Select(r => r.Beat));
        Assert.Equal(new[] { 4, 8, 4 }, enriched.Rows.Select(r => r.Quant));
    }

    [Fact]
    public void QuantizationReadsTheBeatsDenominator()
    {
        var times = new[] { 0.0m, 0.1m, 0.2m, 0.3m, 0.4m };
        var beats = new[] { 0m, 0.25m, 1m / 3m, 0.125m, 0.123m };
        var snapshot = Snapshot(Taps(times));
        var ssc = new StepChartData(5, true,
            times.Select((t, i) => new StepRow(beats[i], 1) { Time = t }).ToArray(),
            Array.Empty<StepHold>(), Array.Empty<StepTick>());

        var enriched = StepChartEnricher.Enrich(snapshot, ssc, Counts(5));

        Assert.Equal(new[] { 4, 16, 12, 32, 0 }, enriched.Rows.Select(r => r.Quant));
    }

    [Fact]
    public void ARowCountMismatchRefusesTheBeats()
    {
        var snapshot = Snapshot(Taps(0.0m, 0.5m));
        var ssc = new StepChartData(5, true,
            new[] { new StepRow(0m, 1) { Time = 0.0m } },
            Array.Empty<StepHold>(), Array.Empty<StepTick>());

        var enriched = StepChartEnricher.Enrich(snapshot, ssc, Counts(2));

        Assert.False(enriched.BeatsAligned);
        Assert.All(enriched.Rows, r => Assert.Null(r.Beat));
    }

    [Fact]
    public void ATimeDriftRefusesTheBeats()
    {
        var snapshot = Snapshot(Taps(0.0m, 0.5m));
        var ssc = new StepChartData(5, true,
            new[]
            {
                new StepRow(0m, 1) { Time = 0.0m },
                new StepRow(1m, 1) { Time = 0.6m }
            },
            Array.Empty<StepHold>(), Array.Empty<StepTick>());

        Assert.False(StepChartEnricher.Enrich(snapshot, ssc, Counts(2)).BeatsAligned);
    }

    [Fact]
    public void SscTicksAdoptOnlyWhenTheyReproduceTheAuthoredSum()
    {
        var snapshot = Snapshot(Taps(0.0m, 0.5m),
            ticks: new[] { new SnapshotTickSpan(1m, 2m, 3) });
        StepChartData Ssc(params StepTick[] ticks)
        {
            return new StepChartData(5, true,
                new[]
                {
                    new StepRow(0m, 1) { Time = 0.0m },
                    new StepRow(1m, 1) { Time = 0.5m }
                },
                Array.Empty<StepHold>(), ticks);
        }

        var adopted = StepChartEnricher.Enrich(snapshot,
            Ssc(new StepTick(2m, 1.1m, 0), new StepTick(3m, 1.6m, 0), new StepTick(4m, 2.1m, 0)),
            Counts(5));
        Assert.Equal(new[] { 1.1m, 1.6m, 2.1m }, adopted.TickTimes);

        // Two ssc checkpoints against an authored three: the grid math disagrees with the
        // generator, so the spans spread instead.
        var refused = StepChartEnricher.Enrich(snapshot,
            Ssc(new StepTick(2m, 1.1m, 0), new StepTick(3m, 1.6m, 0)),
            Counts(5));
        Assert.Equal(new[] { 1m, 1.5m, 2m }, refused.TickTimes);
    }

    [Fact]
    public void SpreadTicksSitEvenlyAndALoneTickSitsMidSpan()
    {
        var snapshot = Snapshot(Taps(0.0m),
            ticks: new[]
            {
                new SnapshotTickSpan(10m, 11m, 1),
                new SnapshotTickSpan(20m, 22m, 3)
            });

        var enriched = StepChartEnricher.Enrich(snapshot, null, Counts(5));

        Assert.Equal(new[] { 10.5m, 20m, 21m, 22m }, enriched.TickTimes);
    }
}
