using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     A band's floor is exactly where <see cref="ThemeScales.BandFor" /> cuts it, so a section named
///     for the range a band covers prints the numbers its colour is chosen by
///     (docs/design/pumbility-overhaul.md D66).
/// </summary>
public sealed class RarityBandFloorTests
{
    [Theory]
    [InlineData(RarityBand.Silver)]
    [InlineData(RarityBand.Emerald)]
    [InlineData(RarityBand.Gold)]
    [InlineData(RarityBand.Sapphire)]
    [InlineData(RarityBand.Prism)]
    public void EveryBandStartsExactlyAtItsFloor(RarityBand band)
    {
        var floor = ThemeScales.FloorOf(band);

        Assert.Equal(band, ThemeScales.BandFor(floor));
        Assert.Equal(band - 1, ThemeScales.BandFor(floor - 1e-9));
    }

    [Fact]
    public void CommonStartsAtZero()
    {
        Assert.Equal(0.0, ThemeScales.FloorOf(RarityBand.Common));
        Assert.Equal(RarityBand.Common, ThemeScales.BandFor(0));
    }
}
