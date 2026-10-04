using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     A board colors a printed place by its share of the field at or below it, so first place is
///     the top of the rarity ramp on a board of any size — a sole entrant included — and the size
///     of the board only decides how the places under it spread.
/// </summary>
public sealed class PlaceColorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(500)]
    public void FirstPlaceIsPrismOnABoardOfAnySize(int fieldSize)
    {
        Assert.Equal(1.0, ThemeScales.PlaceFraction(1, fieldSize));
        Assert.Equal("color:var(--rarity-prism);", ThemeScales.PlaceStyle(1, fieldSize));
    }

    [Fact]
    public void LastOfAHundredIsCommon()
    {
        Assert.Equal("color:var(--rarity-common);", ThemeScales.PlaceStyle(100, 100));
    }

    [Theory]
    [InlineData(2, 4, "color:var(--rarity-gold);")]
    [InlineData(3, 4, "color:var(--rarity-emerald);")]
    [InlineData(4, 4, "color:var(--rarity-silver);")]
    public void PlacesUnderFirstSpreadByTheirShareOfTheField(int place, int fieldSize, string expected)
    {
        Assert.Equal(expected, ThemeScales.PlaceStyle(place, fieldSize));
    }

    [Theory]
    [InlineData(9, 8)]
    [InlineData(40, 8)]
    [InlineData(1, 0)]
    [InlineData(0, 8)]
    public void APlaceOutsideTheFieldOrAnEmptyFieldIsCommon(int place, int fieldSize)
    {
        Assert.Equal(0.0, ThemeScales.PlaceFraction(place, fieldSize));
        Assert.Equal("color:var(--rarity-common);", ThemeScales.PlaceStyle(place, fieldSize));
    }
}
