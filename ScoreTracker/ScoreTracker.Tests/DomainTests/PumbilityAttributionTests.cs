using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.PlayerProgress.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Splitting a batch's PUMBILITY movement across the charts that caused it. The property
///     that matters most is the last one: whatever the split, it adds up to the pool's real
///     movement, because the ceremony band prints that total right above these numbers.
/// </summary>
public sealed class PumbilityAttributionTests
{
    private const int Pool = 3;

    [Fact]
    public void AChartThatKeptItsSeatIsWorthItsWholeImprovement()
    {
        // Nothing had to leave to make room, so every point it gained reached the total.
        var a = Guid.NewGuid();
        var gains = PumbilityAttribution.GainsPerChart(new[]
        {
            Priced(a, 800, 850),
            Priced(Guid.NewGuid(), 700, 700),
            Priced(Guid.NewGuid(), 600, 600)
        }, Pool);

        Assert.Equal(50, gains[a]);
    }

    [Fact]
    public void AChartThatTookASeatIsWorthOnlyWhatItBeatTheDepartureBy()
    {
        // The naive reading credits the entrant its whole 640. It displaced a 600, so the
        // player's total moved by 40 and that is what the row may claim.
        var entrant = Guid.NewGuid();
        var leaver = Guid.NewGuid();
        var gains = PumbilityAttribution.GainsPerChart(new[]
        {
            Priced(Guid.NewGuid(), 800, 800),
            Priced(Guid.NewGuid(), 700, 700),
            Priced(leaver, 600, 600),
            Priced(entrant, null, 640)
        }, Pool);

        Assert.Equal(40, gains[entrant]);
        Assert.False(gains.ContainsKey(leaver));
    }

    [Fact]
    public void TheStrongestEntrantDisplacesTheWeakestSeat()
    {
        // Two charts arrive at once, so two seats go. Adding the best one first is what pushes
        // out the last seat, which is the order the pool actually falls in.
        var big = Guid.NewGuid();
        var small = Guid.NewGuid();
        var gains = PumbilityAttribution.GainsPerChart(new[]
        {
            Priced(Guid.NewGuid(), 900, 900),
            Priced(Guid.NewGuid(), 700, 700),
            Priced(Guid.NewGuid(), 600, 600),
            Priced(big, null, 950),
            Priced(small, null, 800)
        }, Pool);

        Assert.Equal(350, gains[big]); // 950 − 600, the weakest seat
        Assert.Equal(100, gains[small]); // 800 − 700, the next one
    }

    [Fact]
    public void AnUnfilledPoolCreditsAnEntrantEverythingItBrought()
    {
        var entrant = Guid.NewGuid();
        var gains = PumbilityAttribution.GainsPerChart(new[]
        {
            Priced(Guid.NewGuid(), 800, 800),
            Priced(entrant, null, 500)
        }, Pool);

        // Nothing was displaced — the seat was empty.
        Assert.Equal(500, gains[entrant]);
    }

    [Fact]
    public void AChartOutsideThePoolGainsNothingHoweverMuchItImproved()
    {
        // A big score under the bar is a good play, but it did not move the total, and a badge
        // claiming otherwise is the thing this whole split exists to avoid.
        var outside = Guid.NewGuid();
        var gains = PumbilityAttribution.GainsPerChart(new[]
        {
            Priced(Guid.NewGuid(), 900, 900),
            Priced(Guid.NewGuid(), 800, 800),
            Priced(Guid.NewGuid(), 700, 700),
            Priced(outside, 100, 500)
        }, Pool);

        Assert.False(gains.ContainsKey(outside));
    }

    [Fact]
    public void MovementUnderAWholePointIsStillAGainAndKeepsItsFraction()
    {
        // A whole point used to be the floor here, which discarded the movement AND the only
        // evidence that a fractional gain exists at all. The badge reports what it is given.
        var a = Guid.NewGuid();
        var gains = PumbilityAttribution.GainsPerChart(new[] { Priced(a, 800, 800.4) }, Pool);

        Assert.Equal(0.4, Assert.Single(gains).Value, 6);
    }

    [Fact]
    public void ARegressionIsNeverReportedAsAGain()
    {
        var a = Guid.NewGuid();
        var gains = PumbilityAttribution.GainsPerChart(new[] { Priced(a, 800, 750) }, Pool);

        Assert.Empty(gains);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(50)]
    public void TheSplitAlwaysAddsUpToThePoolsRealMovement(int poolSize)
    {
        // The ceremony band prints the total directly above these rows, so a split that does
        // not reconcile with it is visibly wrong on the page.
        var priced = Enumerable.Range(0, 40)
            .Select(i => Priced(Guid.NewGuid(), i % 3 == 0 ? null : 500 + i * 7, 500 + i * 11))
            .ToArray();

        var gains = PumbilityAttribution.GainsPerChart(priced, poolSize);

        var oldTotal = priced.Where(p => p.Old != null).OrderByDescending(p => p.Old!.Value)
            .Take(poolSize).Sum(p => p.Old!.Value);
        var newTotal = priced.OrderByDescending(p => p.New).Take(poolSize).Sum(p => p.New);

        Assert.Equal(Math.Round(newTotal - oldTotal), gains.Values.Sum(), 0);
    }

    private static PumbilityAttribution.Priced Priced(Guid chartId, double? old, double now)
    {
        return new PumbilityAttribution.Priced(chartId, old, now);
    }
}
