using System;
using System.Linq;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The five variability bands (docs/design/pumbility-overhaul.md D35): the peers' quartile
///     width on the log, cut at ±0.5σ and ±1.5σ against the other charts in the set.
/// </summary>
public sealed class PeerVariabilityTests
{
    [Fact]
    public void TheWidthIsReadOnTheLog()
    {
        Assert.Equal(0, PeerVariability.LogWidth(0));
        Assert.Equal(Math.Log(2), PeerVariability.LogWidth(1_000), 12);
        Assert.Equal(Math.Log(45.771), PeerVariability.LogWidth(44_771), 6);
        // A malformed width (Q3 under Q1) reads as zero rather than throwing on the log.
        Assert.Equal(0, PeerVariability.LogWidth(-500));
    }

    [Theory]
    [InlineData(-2.0, PeerVariabilityLevel.VeryConsistent)]
    [InlineData(-1.5, PeerVariabilityLevel.Consistent)]
    [InlineData(-0.6, PeerVariabilityLevel.Consistent)]
    [InlineData(-0.5, PeerVariabilityLevel.Mixed)]
    [InlineData(0.0, PeerVariabilityLevel.Mixed)]
    [InlineData(0.5, PeerVariabilityLevel.Mixed)]
    [InlineData(0.6, PeerVariabilityLevel.Split)]
    [InlineData(1.5, PeerVariabilityLevel.Split)]
    [InlineData(1.6, PeerVariabilityLevel.VerySplit)]
    public void TheCutsSitAtHalfAndOneAndAHalfDeviations(double z, PeerVariabilityLevel expected)
    {
        Assert.Equal(expected, PeerVariability.LevelFor(z, 0, 1));
    }

    [Fact]
    public void ANarrowerChartReadsMoreConsistentThanAWiderOneInTheSameSet()
    {
        var tight = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var wide = Guid.NewGuid();
        var quartiles = new[]
        {
            (tight, PhoenixScore.From(990_000), PhoenixScore.From(994_000)),
            (middle, PhoenixScore.From(975_000), PhoenixScore.From(987_000)),
            (wide, PhoenixScore.From(940_000), PhoenixScore.From(985_000))
        };

        var bands = PeerVariability.Band(quartiles);

        Assert.True(bands[tight] <= bands[middle]);
        Assert.True(bands[middle] <= bands[wide]);
        Assert.NotEqual(bands[tight], bands[wide]);
    }

    [Fact]
    public void ASingleChartOrIdenticalWidthsAreMixedAndAnEmptySetIsEmpty()
    {
        var one = Guid.NewGuid();
        Assert.Equal(PeerVariabilityLevel.Mixed,
            PeerVariability.Band(new[] { (one, PhoenixScore.From(950_000), PhoenixScore.From(990_000)) })[one]);

        var same = Enumerable.Range(0, 4).Select(_ => (Guid.NewGuid(), PhoenixScore.From(960_000), PhoenixScore.From(980_000))).ToArray();
        Assert.All(PeerVariability.Band(same).Values, level => Assert.Equal(PeerVariabilityLevel.Mixed, level));

        Assert.Empty(PeerVariability.Band(Array.Empty<(Guid, PhoenixScore, PhoenixScore)>()));
    }
}
