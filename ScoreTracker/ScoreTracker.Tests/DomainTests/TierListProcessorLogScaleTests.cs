using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class TierListProcessorLogScaleTests
{
    /// <summary>
    ///     S20's real census against the ~17k cohort: 135 charts, 9 of them in nobody's pool,
    ///     the rest running 175 peers down to 1. Band sizes and their peer ranges are the
    ///     measured ones (docs/design/pumbility-tier-list.md §4b), so the raw-versus-log
    ///     behaviour below is the behaviour on a real folder rather than on a shape invented
    ///     to produce it.
    /// </summary>
    private static Dictionary<Guid, int> MeasuredFolder(out Guid mostPooled, out Guid leastPooled)
    {
        var counts = new Dictionary<Guid, int>();
        void Add(int howMany, int from, int to)
        {
            for (var i = 0; i < howMany; i++)
                counts[Guid.NewGuid()] = howMany == 1 ? from : from + (to - from) * i / (howMany - 1);
        }

        mostPooled = Guid.NewGuid();
        counts[mostPooled] = 175;
        Add(7, 163, 101);   // Staple
        Add(13, 86, 53);    // Strong
        Add(21, 50, 29);    // Solid
        Add(41, 28, 8);     // Average
        Add(20, 7, 4);      // Modest
        Add(13, 3, 2);      // Slim
        Add(9, 1, 1);       // Poor
        leastPooled = Guid.NewGuid();
        counts[leastPooled] = 1;
        Add(9, 0, 0);       // in nobody's pool
        return counts;
    }

    [Fact]
    public void RawCountsStrandTheTwoHardestTiersAndTheLogScaleDoesNot()
    {
        var counts = MeasuredFolder(out _, out _);

        var raw = TierListProcessor.ProcessIntoTierList("Raw", counts).ToArray();
        var logged = TierListProcessor.ProcessIntoLogScaledTierList("Logged", counts).ToArray();

        // The whole reason for the transform: on this distribution mu - 1.5*sigma sits below
        // zero, so no chart can reach the hardest band however few pools hold it.
        Assert.DoesNotContain(raw, e => e.Category == TierListCategory.Underrated);
        Assert.Contains(logged, e => e.Category == TierListCategory.Underrated);
        Assert.Contains(logged, e => e.Category == TierListCategory.VeryHard);
    }

    [Fact]
    public void EveryTierIsReachableOnTheLogScale()
    {
        var counts = MeasuredFolder(out _, out _);

        var byCategory = TierListProcessor.ProcessIntoLogScaledTierList("Logged", counts)
            .GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count());

        for (var category = TierListCategory.Overrated; category <= TierListCategory.Underrated; category++)
            Assert.True(byCategory.ContainsKey(category), $"{category} came out empty");
    }

    [Fact]
    public void TheLogScaleChangesNoOrdering()
    {
        var counts = MeasuredFolder(out _, out _);

        var byCount = counts.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray();
        var logged = TierListProcessor.ProcessIntoLogScaledTierList("Logged", counts)
            .OrderBy(e => e.Order).Select(e => e.ChartId).ToArray();

        Assert.Equal(byCount, logged);
    }

    [Fact]
    public void TheMostPooledChartOutranksTheLeastPooledOne()
    {
        var counts = MeasuredFolder(out var mostPooled, out var leastPooled);

        var logged = TierListProcessor.ProcessIntoLogScaledTierList("Logged", counts)
            .ToDictionary(e => e.ChartId, e => e.Category);

        // The category enum runs easiest (Overrated) to hardest, so more pools must mean a
        // lower value.
        Assert.True(logged[mostPooled] < logged[leastPooled],
            $"175 peers came out {logged[mostPooled]}, 1 peer came out {logged[leastPooled]}");
    }

    [Fact]
    public void ChartsNoPoolHoldsAreUnrecordedAndDoNotMoveTheOtherBands()
    {
        var counts = MeasuredFolder(out _, out _);
        var rated = counts.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

        var withZeros = TierListProcessor.ProcessIntoLogScaledTierList("Logged", counts)
            .ToDictionary(e => e.ChartId, e => e.Category);
        var withoutZeros = TierListProcessor.ProcessIntoLogScaledTierList("Logged", rated)
            .ToDictionary(e => e.ChartId, e => e.Category);

        Assert.Equal(9, withZeros.Count(kv => kv.Value == TierListCategory.Unrecorded));
        foreach (var chartId in rated.Keys)
            Assert.Equal(withoutZeros[chartId], withZeros[chartId]);
    }

    [Fact]
    public void AFolderNobodyPoolsIsEntirelyUnrecorded()
    {
        var counts = new Dictionary<Guid, int>
            { [Guid.NewGuid()] = 0, [Guid.NewGuid()] = 0 };

        var logged = TierListProcessor.ProcessIntoLogScaledTierList("Logged", counts).ToArray();

        Assert.All(logged, e => Assert.Equal(TierListCategory.Unrecorded, e.Category));
    }

    [Fact]
    public void TwoChartsWithTheSameCountBothLandInTheTopTier()
    {
        // The above-range folder (design doc §6): for a cohort well below it, S22 has exactly
        // two charts anyone holds, one peer each. Equal values have no spread, so both sit at
        // their own mean and come out Staple — which is true, they are the only two charts
        // there that can do anything for that player.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var counts = new Dictionary<Guid, int> { [first] = 1, [second] = 1, [Guid.NewGuid()] = 0 };

        var logged = TierListProcessor.ProcessIntoLogScaledTierList("Logged", counts)
            .ToDictionary(e => e.ChartId, e => e.Category);

        Assert.Equal(TierListCategory.Overrated, logged[first]);
        Assert.Equal(TierListCategory.Overrated, logged[second]);
    }

    [Fact]
    public void AnEmptyFolderProducesNoEntries()
    {
        Assert.Empty(TierListProcessor.ProcessIntoLogScaledTierList("Logged", new Dictionary<Guid, int>()));
    }
}
