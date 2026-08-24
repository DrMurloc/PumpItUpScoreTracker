using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The two-sided-video caption (docs/design/video-sides.md): arrows only, the viewed
///     chart's half emphasized, the partner muted — and a partner missing from the selected
///     mix leaves its half empty rather than guessing a level from another mix.
/// </summary>
public sealed class VideoSideCaptionTests : TestContext
{
    public VideoSideCaptionTests()
    {
        var localizer = new Mock<IStringLocalizer<App>>();
        localizer.Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        Services.AddSingleton(localizer.Object);
    }

    private IRenderedComponent<VideoSideCaption> Render(VideoSide side, string own, string? partner)
    {
        return RenderComponent<VideoSideCaption>(p => p
            .Add(c => c.Side, side)
            .Add(c => c.OwnLabel, own)
            .Add(c => c.PartnerLabel, partner));
    }

    [Fact]
    public void TheViewedChartsHalfCarriesTheEmphasisAndThePartnerStaysMuted()
    {
        var cut = Render(VideoSide.Right, "S22", "S17");

        var halves = cut.FindAll(".video-side-half");
        Assert.Equal(2, halves.Count);
        Assert.Contains("S17", halves[0].TextContent);
        Assert.Contains("S22", halves[1].TextContent);
        Assert.Contains("S22", cut.Find(".video-side-on").TextContent);
    }

    [Fact]
    public void ViewingTheLeftChartEmphasizesTheLeftHalf()
    {
        var cut = Render(VideoSide.Left, "S17", "S22");

        Assert.Contains("S17", cut.Find(".video-side-on").TextContent);
        Assert.Contains("◀", cut.Find(".video-side-on").TextContent);
    }

    [Fact]
    public void APartnerMissingFromTheSelectedMixLeavesItsHalfEmpty()
    {
        var cut = Render(VideoSide.Right, "S22", null);

        var halves = cut.FindAll(".video-side-half");
        Assert.Equal(string.Empty, halves[0].TextContent.Trim());
        Assert.Contains("S22", halves[1].TextContent);
    }

    [Fact]
    public void TheSideReadsAsASentenceToScreenReadersWhileTheRowStaysArrowsOnly()
    {
        var cut = Render(VideoSide.Right, "S22", "S17");

        Assert.Contains("right side", cut.Find(".video-side-sr").TextContent);
        Assert.DoesNotContain("right side", cut.Find(".video-side-half:last-child").TextContent);
    }
}
