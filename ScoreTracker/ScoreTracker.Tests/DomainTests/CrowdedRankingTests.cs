using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Whether a chart's official ranking can hide the players who hold it (docs/design/chart-presence-graph.md
///     §8): all 300 places taken, and the lowest score on it at or above the folder's bar.
/// </summary>
public sealed class CrowdedRankingTests
{
    [Theory]
    [InlineData(ChartType.Double, 20, 975_000)]
    [InlineData(ChartType.Double, 21, 975_000)]
    [InlineData(ChartType.Single, 20, 975_000)]
    [InlineData(ChartType.Double, 22, 970_000)]
    [InlineData(ChartType.Double, 23, 970_000)]
    [InlineData(ChartType.Single, 21, 970_000)]
    [InlineData(ChartType.Single, 22, 970_000)]
    [InlineData(ChartType.Double, 24, 960_000)]
    [InlineData(ChartType.Double, 26, 960_000)]
    [InlineData(ChartType.Single, 23, 960_000)]
    [InlineData(ChartType.Single, 25, 960_000)]
    public void AFullRankingHidesHoldersFromItsFoldersBarUp(ChartType type, int level, int bar)
    {
        var chart = Chart(type, level);

        Assert.Equal(bar, CrowdedRanking.Bar(type, level));
        Assert.True(CrowdedRanking.HidesHolders(chart, new OfficialChartRanking(300, bar)));
        Assert.False(CrowdedRanking.HidesHolders(chart, new OfficialChartRanking(300, bar - 1)));
    }

    [Fact]
    public void ARankingWithPlacesLeftHidesNobodyHoweverHighItsLowestScore()
    {
        Assert.False(CrowdedRanking.HidesHolders(Chart(ChartType.Double, 23), new OfficialChartRanking(299, 999_000)));
    }

    private static Chart Chart(ChartType type, int level)
    {
        return new ChartBuilder().WithMix(MixEnum.Phoenix2).WithType(type).WithLevel(level).Build();
    }
}
