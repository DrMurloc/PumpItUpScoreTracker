using ScoreTracker.ChartIntelligence.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The community Pass list's player weights: a pass counts by where the passer's competitive
///     level sits against the folder (docs/design/pumbility-tier-list.md §10a).
/// </summary>
public sealed class PassPeerWeightsTests
{
    [Theory]
    [InlineData(-4, 0)]
    [InlineData(-3, 7)]
    [InlineData(-2, 6)]
    [InlineData(-1, 5)]
    [InlineData(0, 4)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 0)]
    public void AnActivePlayerCountsByTheirOffsetFromTheFolder(int offset, int expected)
    {
        Assert.Equal(expected, PassPeerWeights.For(20, 20 + offset + .5, isActive: true));
    }

    [Theory]
    [InlineData(-4, 0)]
    [InlineData(-3, 0)]
    [InlineData(-2, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 0)]
    public void AnInactivePlayerCountsOnlyAboveTheFolder(int offset, int expected)
    {
        Assert.Equal(expected, PassPeerWeights.For(20, 20 + offset + .5, isActive: false));
    }

    [Fact]
    public void AFractionalLevelFloorsToTheLevelBelow()
    {
        Assert.Equal(5, PassPeerWeights.For(20, 19.9, isActive: true));
    }

    [Fact]
    public void AWholeLevelStaysOnItsOwnLevel()
    {
        Assert.Equal(4, PassPeerWeights.For(20, 20.0, isActive: true));
    }

    [Fact]
    public void APlayerWithNoCompetitiveLevelCountsNothing()
    {
        Assert.Equal(0, PassPeerWeights.For(10, 0, isActive: true));
    }
}
