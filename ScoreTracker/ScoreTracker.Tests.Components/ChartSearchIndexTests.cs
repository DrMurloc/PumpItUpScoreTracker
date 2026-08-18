using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one chart-name search behind every picker and the site search: what a term finds and
///     in what order, including the trailing shorthand that narrows to a difficulty.
/// </summary>
public sealed class ChartSearchIndexTests
{
    private static readonly Chart GargoyleD22 = ChartSlugsTests.BuildChart(song: "Gargoyle", level: 22);
    private static readonly Chart GargoyleS19 =
        ChartSlugsTests.BuildChart(song: "Gargoyle", level: 19, type: ChartType.Single);
    private static readonly Chart GargoyleFullD22 =
        ChartSlugsTests.BuildChart(song: "Gargoyle - FULL SONG -", level: 22);
    private static readonly Chart RockD22 = ChartSlugsTests.BuildChart(song: "Rock the house", level: 22);

    private static readonly ChartSearchIndex Index =
        ChartSearchIndex.Build(new[] { RockD22, GargoyleFullD22, GargoyleD22, GargoyleS19 });

    [Fact]
    public void ADisplayNameIsSongThenDifficulty()
    {
        Assert.Equal("Gargoyle D22", ChartSearchIndex.NameOf(GargoyleD22));
        Assert.True(Index.TryGet("gargoyle d22", out var chart));
        Assert.Same(GargoyleD22, chart);
    }

    [Fact]
    public void AnExactSongNameLeadsThenTypeThenLevel()
    {
        Assert.Equal(new[] { GargoyleS19, GargoyleD22, GargoyleFullD22 }, Index.Search("gargoyle"));
    }

    [Fact]
    public void ATrailingShorthandNarrowsToThatDifficulty()
    {
        // Still a contains-match on the name inside the folder; the exact song name leads.
        Assert.Equal(new[] { GargoyleD22, GargoyleFullD22 }, Index.Search("gargoyle d22"));
        Assert.Equal(new[] { GargoyleS19 }, Index.Search("Gargoyle S19"));
    }

    [Fact]
    public void AShorthandAloneListsTheFolder()
    {
        Assert.Equal(new[] { GargoyleD22, GargoyleFullD22, RockD22 }, Index.Search(" d22"));
    }

    [Fact]
    public void AnEmptyTermIsTheCatalogAlphabetical()
    {
        Assert.Equal(new[] { GargoyleS19, GargoyleD22, GargoyleFullD22, RockD22 }, Index.Search(""));
        Assert.Equal(Index.Search("").Select(ChartSearchIndex.NameOf), Index.SearchNames(""));
    }

    [Fact]
    public void NothingMatchingIsEmptyNotAThrow()
    {
        Assert.Empty(Index.Search("zzz"));
        Assert.Empty(ChartSearchIndex.Empty.Search("gargoyle"));
    }
}
