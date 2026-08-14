using Bunit;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The badge is one img whose URL encodes the rung, so what matters is the encoding: uniform
///     zero-padding (the source's own padding flips at ten — the trap this component exists to
///     bury), the self-hide on a missing file, and rendering nothing when there is no rung at all.
/// </summary>
public sealed class PumbilityLevelBadgeTests : ComponentTestBase
{
    [Fact]
    public void ALowRungBadgePadsItsFileName()
    {
        // 11,050 sits on BRONZE LV.3 — badge index 3, which the source spells "pumbility_3.png"
        // on nothing and "pumbility_03.png" in its own art folder. Ours is always padded.
        var cut = RenderComponent<PumbilityLevelBadge>(p => p
            .Add(x => x.Level, Phoenix2PumbilityLevel.From(11_050)));

        var img = cut.Find("img");
        Assert.Equal("https://piuimages.arroweclip.se/pumbility/p2/pumbility_03.png",
            img.GetAttribute("src"));
    }

    [Fact]
    public void AHighRungBadgeIsStillTwoDigits()
    {
        var cut = RenderComponent<PumbilityLevelBadge>(p => p
            .Add(x => x.Level, Phoenix2PumbilityLevel.From(17_602.69)));

        Assert.Equal("https://piuimages.arroweclip.se/pumbility/p2/pumbility_24.png",
            cut.Find("img").GetAttribute("src"));
    }

    [Fact]
    public void TheAltTextNamesTheRung()
    {
        var cut = RenderComponent<PumbilityLevelBadge>(p => p
            .Add(x => x.Level, Phoenix2PumbilityLevel.From(17_602.69)));

        Assert.Equal("PUMBILITY level [P.B] DIAMOND LV.4", cut.Find("img").GetAttribute("alt"));
    }

    [Fact]
    public void TheCapstoneAltCarriesNoLevelNumber()
    {
        var cut = RenderComponent<PumbilityLevelBadge>(p => p
            .Add(x => x.Level, Phoenix2PumbilityLevel.From(20_100)));

        Assert.Equal("PUMBILITY level ABYSS ABSOLUTE", cut.Find("img").GetAttribute("alt"));
    }

    [Fact]
    public void AMissingFileHidesTheImageInsteadOfBreakingIt()
    {
        var cut = RenderComponent<PumbilityLevelBadge>(p => p
            .Add(x => x.Level, Phoenix2PumbilityLevel.From(17_602.69)));

        // The graceful-404 contract rides an inline handler, so its presence IS the behavior
        // a bUnit render can pin — the browser side is just style.display.
        Assert.Equal("this.style.display='none'", cut.Find("img").GetAttribute("onerror"));
    }

    [Fact]
    public void NoRungRendersNothing()
    {
        var cut = RenderComponent<PumbilityLevelBadge>(p => p
            .Add(x => x.Level, (Phoenix2PumbilityLevel?)null));

        Assert.Empty(cut.Markup.Trim());
    }
}
