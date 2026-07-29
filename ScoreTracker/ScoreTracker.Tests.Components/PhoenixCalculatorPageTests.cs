using System;
using System.Linq;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Pages.Tools;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Phoenix Calculator chain: which steps each route renders, that the mix drives the
///     grade and the level control, and that the value grid marks where you are. The
///     arithmetic itself is pinned in ScoreAnalysisTests — these are the wiring facts
///     (docs/design/phoenix-calculator-redesign.md).
/// </summary>
public sealed class PhoenixCalculatorPageTests : ComponentTestBase
{
    private void UseMix(MixEnum mix)
    {
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSelectedMix()).ReturnsAsync(mix);
        settings.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);
        Services.AddSingleton(settings.Object);
        this.RenderInteractive();
    }

    private IRenderedComponent<PhoenixCalculator> Render(MixEnum mix = MixEnum.Phoenix, string? from = null)
    {
        UseMix(mix);
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo(from is null ? "/PhoenixCalculator" : $"/PhoenixCalculator?from={from}");
        return RenderComponent<PhoenixCalculator>();
    }

    [Fact]
    public void TheScoreRouteIsTheDefaultAndSkipsTheJudgmentStep()
    {
        var page = Render();

        Assert.NotNull(page.Find(".pc-scorefield"));
        Assert.Empty(page.FindAll(".pc-keys"));
        // Two steps, so "what it's worth" is step 2 rather than step 3.
        Assert.Equal(new[] { "1", "2" }, page.FindAll(".pc-n").Select(n => n.TextContent.Trim()));
    }

    [Fact]
    public void TheResultsScreenRouteAddsTheJudgmentStepInFront()
    {
        var page = Render(from: "judgments");

        Assert.NotNull(page.Find(".pc-keys"));
        Assert.Empty(page.FindAll(".pc-scorefield"));
        Assert.Equal(new[] { "1", "2", "3" }, page.FindAll(".pc-n").Select(n => n.TextContent.Trim()));
    }

    // One render per test: bUnit's TestContext refuses new service registrations once
    // anything has been resolved from it, so a second Render() in the same test throws.
    [Theory]
    [InlineData(null)]
    [InlineData("judgments")]
    public void BothRoutesShareTheValueStep(string? from)
    {
        Assert.NotNull(Render(from: from).Find(".pc-worth"));
    }

    [Fact]
    public void PhoenixOffersALevelSlider()
    {
        var page = Render();

        Assert.NotNull(page.Find(".pc-levelrow input[type=range]"));
        Assert.Empty(page.FindAll(".folder-picker"));
    }

    [Fact]
    public void Phoenix2OffersAFolderPickerInstead()
    {
        // Singles price one level up the curve there, so the type is part of the folder.
        var page = Render(MixEnum.Phoenix2);

        Assert.NotNull(page.Find(".folder-picker"));
        Assert.Empty(page.FindAll(".pc-levelrow"));
    }

    [Fact]
    public void PhoenixShowsNoPlatePicker()
    {
        // Phoenix PUMBILITY ignores the plate, so the page says so instead of rendering a
        // control that changes nothing.
        Assert.Empty(Render().FindAll(".pc-plate"));
    }

    [Fact]
    public void Phoenix2ShowsEveryPlate()
    {
        Assert.Equal(8, Render(MixEnum.Phoenix2).FindAll(".pc-plate").Count);
    }

    // 917,168 is AA in Phoenix but only A+ in Phoenix 2, whose sub-AAA floors were re-cut —
    // the same number reading as two different grades is the reason one mix control drives
    // the whole page.
    [Theory]
    [InlineData(MixEnum.Phoenix, "924,999")]
    [InlineData(MixEnum.Phoenix2, "919,999")]
    public void TheSameScoreGradesDifferentlyPerMix(MixEnum mix, string expectedCeiling)
    {
        var band = Render(mix).Find(".pc-band").TextContent;

        Assert.Contains("900,000", band);
        Assert.Contains(expectedCeiling, band);
    }

    // @((int)Score).ToString("N0") closes the expression at the cast and ships the rest as
    // literal text — it compiles, renders, and reads as "917168.ToString("N0")" on the page.
    [Theory]
    [InlineData(null)]
    [InlineData("judgments")]
    public void NoRazorExpressionLeaksItsOwnSourceAsText(string? from)
    {
        Assert.DoesNotContain("ToString", Render(from: from).Markup);
    }

    [Fact]
    public void TheScoreRendersAsAFormattedNumber()
    {
        Assert.Equal("917,168", Render(from: "judgments").Find(".pc-score").TextContent.Trim());
    }

    [Fact]
    public void TheResultsScreenRouteShowsBothHalvesOfTheMillion()
    {
        var page = Render(from: "judgments");
        var heads = page.FindAll(".pc-barhead").Select(h => h.TextContent).ToArray();

        Assert.Equal(2, heads.Length);
        Assert.Equal(2, page.FindAll(".pc-track").Count);
        Assert.Equal(2, page.FindAll(".pc-legend").Count);
    }

    [Fact]
    public void ThePerfectSegmentIsAlwaysVisibleOnTheEarnedBar()
    {
        // The whole point of the two-candidate baseline: perfects must never be clipped
        // entirely off the left of the window.
        var page = Render(from: "judgments");
        var earned = page.FindAll(".pc-track")[0];

        Assert.Contains(earned.Children, c => (c.GetAttribute("title") ?? string.Empty).StartsWith("Perfect"));
    }

    [Fact]
    public void TheLostBarNeverCreditsPerfects()
    {
        var page = Render(from: "judgments");
        var lost = page.FindAll(".pc-legend")[1];

        Assert.DoesNotContain("Perfect", lost.TextContent);
        Assert.Contains("Miss", lost.TextContent);
    }

    [Fact]
    public void TheValueGridIsCollapsedUntilAskedFor()
    {
        var page = Render();
        Assert.Empty(page.FindAll(".pc-grid"));

        page.Find(".pc-toggle").Click();

        Assert.NotNull(page.Find(".pc-grid"));
        // Sixteen grades across, levels 10 up the game ceiling down the side.
        Assert.Equal(16, page.FindAll(".pc-grid thead th").Count - 1);
        Assert.Equal(20, page.FindAll(".pc-grid tbody tr").Count);
    }

    [Fact]
    public void TheGridMarksWhereYouAreAndLocksOnClick()
    {
        var page = Render();
        page.Find(".pc-toggle").Click();

        Assert.Single(page.FindAll(".pc-cell-here"));

        page.FindAll(".pc-cell")[0].Click();

        Assert.Single(page.FindAll(".pc-cell-locked"));
        Assert.NotEmpty(page.FindAll(".pc-cell-band"));
    }

    [Fact]
    public void Phoenix2SinglesStopTheGridAtTwentySix()
    {
        var page = Render(MixEnum.Phoenix2);
        page.Find(".pc-toggle").Click();

        var levels = page.FindAll(".pc-grid tbody th").Select(t => t.TextContent.Trim()).ToArray();

        Assert.Equal("S26", levels.First());
        Assert.Equal("S10", levels.Last());
    }

    [Fact]
    public void NeighbourChipsListThreeFoldersEitherSide()
    {
        Assert.Equal(6, Render().FindAll(".pc-neighbours .pc-chip").Count);
    }

    [Fact]
    public void BothCommunityCreditsStayInTheFooter()
    {
        // The test localizer echoes keys rather than English, so the assertion names the
        // keys: MR_WEQ's is the formula shout-out, daryen's the score ranges.
        var footer = Render().Find(".pc-refs").TextContent;

        Assert.Contains("Score Formula Shoutout", footer);
        Assert.Contains("Score Range Shoutout", footer);
    }
}
