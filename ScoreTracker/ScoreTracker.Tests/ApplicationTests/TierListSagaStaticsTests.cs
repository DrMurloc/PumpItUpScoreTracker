using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TierListSagaStaticsTests
{
    [Fact]
    public void ProcessIntoTierListReturnsEmptyForEmptyInput()
    {
        var result = TierListSaga.ProcessIntoTierList("Pass Count", new Dictionary<Guid, int>());
        Assert.Empty(result);
    }

    [Fact]
    public void ProcessIntoTierListAssignsTierListNameToEveryEntry()
    {
        var weights = MakeWeights(20, _ => 1);
        var entries = TierListSaga.ProcessIntoTierList("Pass Count", weights).ToArray();
        Assert.All(entries, e => Assert.Equal("Pass Count", (string)e.TierListName));
    }

    [Fact]
    public void ProcessIntoTierListAssignsContiguousAscendingOrder()
    {
        var weights = MakeWeights(20, i => i);
        var entries = TierListSaga.ProcessIntoTierList("Pass Count", weights)
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

        var result = TierListSaga.ProcessIntoTierList("Pass Count", weights).ToArray();
        var entry = result.Single(e => e.ChartId == zeroChart);

        Assert.Equal(TierListCategory.Unrecorded, entry.Category);
    }

    [Fact]
    public void HighestNonZeroWeightFallsAtOrAboveOverrated()
    {
        var weights = MakeWeights(20, _ => 1);
        var topChart = Guid.NewGuid();
        weights[topChart] = 1_000_000;

        var result = TierListSaga.ProcessIntoTierList("Pass Count", weights).ToArray();
        var entry = result.Single(e => e.ChartId == topChart);

        Assert.Equal(TierListCategory.Overrated, entry.Category);
    }

    [Fact]
    public void DistinctChartsProduceDistinctEntries()
    {
        var weights = MakeWeights(50, i => i);
        var result = TierListSaga.ProcessIntoTierList("Pass Count", weights).ToArray();

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

    // ---- Multi-user ProcessIntoTierList(IDictionary<string, IDictionary<Guid, PhoenixScore>>, ...) ----
    //
    // Each user's group is skipped when scoresDict.Count < 5, so test users must score
    // at least 5 distinct charts. Three filler charts at the median bracket the two
    // charts under examination so per-user mean/stddev is stable.

    private static (Guid extreme, Guid mid1, Guid mid2, Guid mid3) FillerChartIds() =>
        (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static IDictionary<Guid, PhoenixScore> UserBoard(Guid easyChart, Guid hardChart,
        Guid c, Guid d, Guid e, int easyScore = 1000000, int hardScore = 0, int fillerScore = 500000) =>
        new Dictionary<Guid, PhoenixScore>
        {
            [easyChart] = (PhoenixScore)easyScore,
            [hardChart] = (PhoenixScore)hardScore,
            [c] = (PhoenixScore)fillerScore,
            [d] = (PhoenixScore)fillerScore,
            [e] = (PhoenixScore)fillerScore
        };

    [Fact]
    public void MultiUserProcessIntoTierListClassifiesUnanimouslyHighScoresAsVeryEasyAndLowAsVeryHard()
    {
        var easy = Guid.NewGuid();
        var hard = Guid.NewGuid();
        var (_, c, d, e) = FillerChartIds();
        var userScores = Enumerable.Range(0, 5).ToDictionary(
            i => $"u{i}",
            i => (IDictionary<Guid, PhoenixScore>)UserBoard(easy, hard, c, d, e));

        var entries = TierListSaga.ProcessIntoTierList(userScores, DifficultyLevel.From(20), "Scores").ToArray();

        Assert.Equal(TierListCategory.VeryEasy, entries.Single(en => en.ChartId == easy).Category);
        Assert.Equal(TierListCategory.VeryHard, entries.Single(en => en.ChartId == hard).Category);
        Assert.All(entries, en => Assert.Equal("Scores", (string)en.TierListName));
    }

    [Fact]
    public void MultiUserProcessIntoTierListSkipsUsersWhoScoredFewerThanFiveCharts()
    {
        // Each user has only 2 charts (< 5), so every group is skipped, chartCount stays 0,
        // averages are NaN, and every chart falls through to the default Underrated bucket.
        var chartA = Guid.NewGuid();
        var chartB = Guid.NewGuid();
        var userScores = Enumerable.Range(0, 5).ToDictionary(
            i => $"u{i}",
            i => (IDictionary<Guid, PhoenixScore>)new Dictionary<Guid, PhoenixScore>
            {
                [chartA] = (PhoenixScore)990000,
                [chartB] = (PhoenixScore)100000
            });

        var entries = TierListSaga.ProcessIntoTierList(userScores, DifficultyLevel.From(20), "Scores").ToArray();

        Assert.All(entries, en => Assert.Equal(TierListCategory.Underrated, en.Category));
    }

    [Fact]
    public void MultiUserProcessIntoTierListWeightsHeavyGroupsHigherThanLightGroups()
    {
        // 5 light users + 5 heavy users with reversed score profiles. The heavy group's 100x
        // weight should drive the final classification toward what *they* saw — chartA hard,
        // chartB easy — rather than the opposing light view.
        var chartA = Guid.NewGuid();
        var chartB = Guid.NewGuid();
        var (_, c, d, e) = FillerChartIds();

        var light = Enumerable.Range(0, 5).ToDictionary(
            i => $"light{i}",
            i => (IDictionary<Guid, PhoenixScore>)UserBoard(chartA, chartB, c, d, e));
        var heavy = Enumerable.Range(0, 5).ToDictionary(
            i => $"heavy{i}",
            i => (IDictionary<Guid, PhoenixScore>)UserBoard(chartB, chartA, c, d, e));

        var combined = light.Concat(heavy).ToDictionary(kv => kv.Key, kv => kv.Value);
        var weights = combined.ToDictionary(kv => kv.Key, kv => kv.Key.StartsWith("heavy") ? 100.0 : 1.0);

        var entries = TierListSaga.ProcessIntoTierList(combined, DifficultyLevel.From(20), "Scores", weights).ToArray();

        Assert.Equal(TierListCategory.VeryHard, entries.Single(en => en.ChartId == chartA).Category);
        Assert.Equal(TierListCategory.VeryEasy, entries.Single(en => en.ChartId == chartB).Category);
    }
}
