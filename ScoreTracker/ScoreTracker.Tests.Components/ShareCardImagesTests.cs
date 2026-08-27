using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The one spelling of a share card's image URLs. The bubble rule is the page's own — a
///     downloaded card that asked for a different picture than DifficultyBubble renders would
///     print art the player has never seen beside that chart.
/// </summary>
public sealed class ShareCardImagesTests
{
    [Fact]
    public void AModernMixKeepsItsPerMixBubbleArt()
    {
        Assert.Equal("https://piuimages.arroweclip.se/difficulty/Phoenix2/s23.png",
            ShareCardImages.DifficultyBubble(MixEnum.Phoenix2, ChartType.Single, DifficultyLevel.From(23)));
        Assert.Equal("https://piuimages.arroweclip.se/difficulty/Phoenix/coop3.png",
            ShareCardImages.DifficultyBubble(MixEnum.Phoenix, ChartType.CoOp, DifficultyLevel.From(3)));
    }

    [Fact]
    public void TheSetsThatPredatePerMixArtAreServedFlat()
    {
        // Every legacy mix reuses the XX images, and SP/DP were always flat.
        Assert.Equal("https://piuimages.arroweclip.se/difficulty/d21.png",
            ShareCardImages.DifficultyBubble(MixEnum.XX, ChartType.Double, DifficultyLevel.From(21)));
        Assert.Equal("https://piuimages.arroweclip.se/difficulty/s18.png",
            ShareCardImages.DifficultyBubble(MixEnum.Prime2, ChartType.Single, DifficultyLevel.From(18)));
        Assert.Equal("https://piuimages.arroweclip.se/difficulty/sp3.png",
            ShareCardImages.DifficultyBubble(MixEnum.Phoenix, ChartType.SinglePerformance, DifficultyLevel.From(3)));
    }

    [Fact]
    public void AChartThePageDrawsAsALegacyChipHasNoBubble()
    {
        // Pre-Exceed slots, Half-Double and levelled legacy co-ops have no bubble art in any
        // set — the page renders a chip instead, so the card prints nothing rather than a guess.
        Assert.Null(ShareCardImages.DifficultyBubble(ChartOf(MixEnum.Nx2, ChartType.Single, 7, LegacySlot.Crazy)));
        Assert.Null(ShareCardImages.DifficultyBubble(ChartOf(MixEnum.Zero, ChartType.HalfDouble, 9)));
        Assert.Null(ShareCardImages.DifficultyBubble(ChartOf(MixEnum.Fiesta2, ChartType.CoOp, 15, players: 2)));
    }

    [Fact]
    public void AModernChartCarriesItsOwnBubble()
    {
        Assert.Equal("https://piuimages.arroweclip.se/difficulty/Phoenix2/d26.png",
            ShareCardImages.DifficultyBubble(ChartOf(MixEnum.Phoenix2, ChartType.Double, 26)));
    }

    private static Chart ChartOf(MixEnum mix, ChartType type, int level, LegacySlot? slot = null, int? players = null) =>
        new(Guid.NewGuid(), mix,
            new Song(Name.From("Song"), SongType.Arcade, new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2),
                Name.From("Artist"), 180),
            type, DifficultyLevel.From(level), mix, null, null, slot, players);
}
