using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Pages.Tools;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Life Calculator bench: the bar, the judgment keys, the step toggle and the
///     Phoenix 2 overflow note. The arithmetic itself is pinned in LifebarAnalysisTests —
///     these are the wiring facts (docs/design/life-calculator-redesign.md).
/// </summary>
public sealed class LifeCalculatorPageTests : ComponentTestBase
{
    public LifeCalculatorPageTests()
    {
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix);
        // No theme override: the page falls through to the selected mix for its chart palette.
        settings.Setup(s => s.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);
        Services.AddSingleton(settings.Object);
        JSInterop.SetupModule("./js/life-calculator.js").SetupVoid("countTo", _ => true);
        this.RenderInteractive();
    }

    /// <summary>
    ///     The page hosts an ApexChart, whose own JS interop leaves work parked on the
    ///     renderer's dispatcher under bUnit. A bare <c>Click()</c> queues behind it and the
    ///     handler never runs inside the test; going through <see cref="IRenderedFragment.InvokeAsync" />
    ///     pumps the dispatcher, so the click actually lands.
    /// </summary>
    private static Task Click(IRenderedComponent<LifeCalculator> page, string selector) =>
        page.InvokeAsync(() => page.Find(selector).Click());

    private static Task ClickAt(IRenderedComponent<LifeCalculator> page, string selector, int index) =>
        page.InvokeAsync(() => page.FindAll(selector)[index].Click());

    private static Task ClickWithText(IRenderedComponent<LifeCalculator> page, string selector, string text) =>
        page.InvokeAsync(() => page.FindAll(selector).First(e => e.TextContent.Contains(text)).Click());

    private static int Life(IRenderedComponent<LifeCalculator> page) =>
        int.Parse(page.Find(".lc-life").TextContent.Replace(",", string.Empty).Trim());

    private static Task Press(IRenderedComponent<LifeCalculator> page, string judgment) =>
        Click(page, $".lc-key-{judgment}");

    private static string Tele(IRenderedComponent<LifeCalculator> page, int index) =>
        page.FindAll(".lc-tele-value")[index].TextContent.Trim();

    /// <summary>
    ///     Opens where a run opens. On a full bar the first perfect anyone tries does nothing
    ///     — the bar is already clamped at max — which reads as a broken page.
    /// </summary>
    [Fact]
    public void OpensAtSongStartNotOnAFullBar()
    {
        var page = RenderComponent<LifeCalculator>();

        Assert.Equal(500, Life(page));
        Assert.Equal("ok", page.Find(".lc-state").GetAttribute("data-state"));
        Assert.Equal("false", page.Find(".lc-note").GetAttribute("data-live"));
    }

    [Fact]
    public async Task APerfectAtSongStartActuallyMovesTheBar()
    {
        var page = RenderComponent<LifeCalculator>();

        await Press(page, "perfect");

        Assert.True(Life(page) > 500);
    }

    [Fact]
    public async Task AMissFromAFullBarCosts270()
    {
        var page = RenderComponent<LifeCalculator>();
        await ClickWithText(page, ".lc-ghost", "Fill");
        // Level 23 tops out at 1000 + 23*23*3.
        Assert.Equal(2587, Life(page));

        await Press(page, "miss");

        Assert.Equal(2317, Life(page));
    }

    [Fact]
    public async Task TheStepToggleMultipliesTheWholePress()
    {
        var page = RenderComponent<LifeCalculator>();
        await ClickWithText(page, ".lc-ghost", "Fill");
        await ClickWithText(page, ".lc-seg button", "20");

        await Press(page, "bad");

        // Twenty bads at a flat -50 each.
        Assert.Equal(2587 - 20 * 50, Life(page));
        Assert.Equal("0", Tele(page, 0));
    }

    /// <summary>
    ///     Dragging the slider rebuilt the simulator by replaying bads down to the old life,
    ///     which overshoots in steps of 50 — so every tick of the drag quietly bled life.
    /// </summary>
    [Fact]
    public async Task DraggingTheLevelSliderDoesNotChangeLife()
    {
        var page = RenderComponent<LifeCalculator>();
        await Press(page, "perfect");
        var before = Life(page);
        var multiplier = Tele(page, 1);

        // oninput, not onchange: the slider updates live as you drag, which is exactly the
        // path that used to bleed life on every tick.
        foreach (var level in new[] { 24, 25, 26, 25, 24, 23, 22, 21 })
            await page.InvokeAsync(() => page.Find("#lc-level").Input(level.ToString()));

        Assert.Equal(before, Life(page));
        Assert.Equal(multiplier, Tele(page, 1));
    }

    [Fact]
    public async Task DroppingToALowerLevelClampsLifeToTheNewMaximum()
    {
        var page = RenderComponent<LifeCalculator>();
        await ClickWithText(page, ".lc-ghost", "Fill");
        Assert.Equal(2587, Life(page));

        // Level 1 tops out at 1003.
        await ClickAt(page, ".lc-ladder-row", 0);

        Assert.Equal(1003, Life(page));
    }

    [Fact]
    public async Task TheStepBadgeOnlyShowsWhenTheStepIsMoreThanOne()
    {
        var page = RenderComponent<LifeCalculator>();
        Assert.Empty(page.FindAll(".lc-key-step"));

        await ClickWithText(page, ".lc-seg button", "50");

        Assert.Equal(5, page.FindAll(".lc-key-step").Count);
        Assert.All(page.FindAll(".lc-key-step"), el => Assert.Contains("50", el.TextContent));
    }

    /// <summary>
    ///     The insight the whole bench exists for: a miss zeroes the multiplier, so the very
    ///     next perfect pays nothing at all.
    /// </summary>
    [Fact]
    public async Task AMissLeavesTheNextPerfectPayingNothing()
    {
        var page = RenderComponent<LifeCalculator>();

        await Press(page, "miss");
        var afterMiss = Life(page);
        Assert.Equal("0.00", Tele(page, 1));

        await Press(page, "perfect");

        // The perfect pays no life at all — it only buys back 0.02 of the multiplier.
        Assert.Equal(afterMiss, Life(page));
        Assert.Equal("0.02", Tele(page, 1));
    }

    [Fact]
    public async Task GoodsMoveNeitherLifeNorMultiplier()
    {
        var page = RenderComponent<LifeCalculator>();
        var before = Life(page);
        var multiplier = Tele(page, 1);

        await Press(page, "good");

        Assert.Equal(before, Life(page));
        Assert.Equal(multiplier, Tele(page, 1));
    }

    [Fact]
    public async Task ThePhoenix2NoteLightsUpOnlyAtFullOverflow()
    {
        var page = RenderComponent<LifeCalculator>();
        Assert.Equal("false", page.Find(".lc-note").GetAttribute("data-live"));

        await ClickWithText(page, ".lc-ghost", "Fill");
        Assert.Equal("true", page.Find(".lc-note").GetAttribute("data-live"));
        Assert.Equal("overflow-full", page.Find(".lc-state").GetAttribute("data-state"));

        await Press(page, "miss");

        Assert.Equal("false", page.Find(".lc-note").GetAttribute("data-live"));
        Assert.Equal("into-overflow", page.Find(".lc-state").GetAttribute("data-state"));
    }

    [Fact]
    public async Task TheLadderRowsPickTheLevel()
    {
        var page = RenderComponent<LifeCalculator>();
        Assert.NotNull(page.Find(".lc-zone-overflow"));

        await ClickAt(page, ".lc-ladder-row", 0);

        Assert.Equal("1", page.Find(".lc-level-value").TextContent.Trim());
    }

    [Fact]
    public async Task TheBudgetLeadsWithTheCliffAndSaysWhenAThresholdCannotBeBought()
    {
        var page = RenderComponent<LifeCalculator>();

        var tiles = page.FindAll(".lc-bud");
        Assert.Equal(4, tiles.Count);
        Assert.Contains("18", tiles[0].QuerySelector(".lc-bud-value")!.TextContent);

        // Level 3's overflow (27) is thinner than one miss, so the rainbow can't be held.
        await ClickAt(page, ".lc-ladder-row", 2);

        var lowTiles = page.FindAll(".lc-bud");
        Assert.Contains("Impossible", lowTiles[2].TextContent);
        Assert.Contains("18", lowTiles[0].QuerySelector(".lc-bud-value")!.TextContent);
    }

    [Fact]
    public async Task SwitchingChartViewsMovesThePressedState()
    {
        var page = RenderComponent<LifeCalculator>();
        Assert.Equal("true", page.FindAll(".lc-seg button")
            .First(b => b.TextContent.Contains("settles")).GetAttribute("aria-pressed"));

        await ClickWithText(page, ".lc-seg button", "survive");

        Assert.Equal("true", page.FindAll(".lc-seg button")
            .First(b => b.TextContent.Contains("survive")).GetAttribute("aria-pressed"));
        Assert.Equal("false", page.FindAll(".lc-seg button")
            .First(b => b.TextContent.Contains("settles")).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void TheLadderListsEveryLevelAndMarksTheCurrentOne()
    {
        var page = RenderComponent<LifeCalculator>();

        var rows = page.FindAll(".lc-ladder-row");
        Assert.Equal(29, rows.Count);
        var current = Assert.Single(rows, r => r.GetAttribute("data-current") == "true");
        Assert.Equal("23", current.QuerySelector(".lc-ladder-level")!.TextContent.Trim());
    }

    [Fact]
    public async Task TheProvenanceNoteIsCollapsedUntilAskedFor()
    {
        var page = RenderComponent<LifeCalculator>();
        Assert.Empty(page.FindAll(".lc-provenance-body"));

        await Click(page, ".lc-provenance");

        Assert.Contains("Team Infinitesimal", page.Find(".lc-provenance-body").TextContent);
    }
}
