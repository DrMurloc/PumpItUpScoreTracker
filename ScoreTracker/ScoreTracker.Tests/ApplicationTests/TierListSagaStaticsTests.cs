using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TierListSagaStaticsTests
{
    [Fact]
    public void ProcessIntoTierListReturnsEmptyForEmptyInput()
    {
        var result = TierListSaga.ProcessIntoTierList("Bounties", new Dictionary<Guid, int>());
        Assert.Empty(result);
    }

    [Fact]
    public void ProcessIntoTierListAssignsTierListNameToEveryEntry()
    {
        var weights = MakeWeights(20, _ => 1);
        var entries = TierListSaga.ProcessIntoTierList("Bounties", weights).ToArray();
        Assert.All(entries, e => Assert.Equal("Bounties", (string)e.TierListName));
    }

    [Fact]
    public void ProcessIntoTierListAssignsContiguousAscendingOrder()
    {
        var weights = MakeWeights(20, i => i);
        var entries = TierListSaga.ProcessIntoTierList("Bounties", weights)
            .OrderBy(e => e.Order)
            .ToArray();

        Assert.Equal(weights.Count, entries.Length);
        for (var i = 0; i < entries.Length; i++)
            Assert.Equal(i, entries[i].Order);
    }

    [Fact]
    public void ZeroWeightedChartIsCategorisedAsUnrecorded()
    {
        var weights = MakeWeights(20, i => i + 1);
        var zeroChart = Guid.NewGuid();
        weights[zeroChart] = 0;

        var result = TierListSaga.ProcessIntoTierList("Bounties", weights).ToArray();
        var entry = result.Single(e => e.ChartId == zeroChart);

        Assert.Equal(TierListCategory.Unrecorded, entry.Category);
    }

    [Fact]
    public void HighestNonZeroWeightFallsAtOrAboveOverrated()
    {
        var weights = MakeWeights(20, _ => 1);
        var topChart = Guid.NewGuid();
        weights[topChart] = 1_000_000;

        var result = TierListSaga.ProcessIntoTierList("Bounties", weights).ToArray();
        var entry = result.Single(e => e.ChartId == topChart);

        Assert.Equal(TierListCategory.Overrated, entry.Category);
    }

    [Fact]
    public void DistinctChartsProduceDistinctEntries()
    {
        var weights = MakeWeights(50, i => i);
        var result = TierListSaga.ProcessIntoTierList("Bounties", weights).ToArray();

        Assert.Equal(weights.Count, result.Length);
        Assert.Equal(weights.Count, result.Select(r => r.ChartId).Distinct().Count());
    }

    [Theory]
    [InlineData(new double[] { 1, 1, 1, 1 }, false, 0)]
    [InlineData(new double[] { 1, 2, 3, 4 }, false, 1.118033988749895)]
    public void StdDevReturnsExpectedPopulationDeviation(double[] values, bool asSample, double expected)
    {
        Assert.Equal(expected, TierListSaga.StdDev(values, asSample), 6);
    }

    [Fact]
    public void StdDevAsSampleDiffersFromPopulation()
    {
        double[] values = { 1, 2, 3, 4, 5 };
        var population = TierListSaga.StdDev(values, false);
        var sample = TierListSaga.StdDev(values, true);
        Assert.True(sample > population);
    }

    private static IDictionary<Guid, int> MakeWeights(int count, Func<int, int> weightFor)
    {
        var dict = new Dictionary<Guid, int>();
        for (var i = 0; i < count; i++) dict[Guid.NewGuid()] = weightFor(i);
        return dict;
    }
}
