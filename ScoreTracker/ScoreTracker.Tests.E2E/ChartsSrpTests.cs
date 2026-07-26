using System.Text.RegularExpressions;
using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The /Charts SRP's one critical whole-workflow path (docs/design/charts-srp.md C14):
///     a filtered URL lands filtered, live filtering rewrites the URL through history
///     interop, and a card is a real link to the canonical chart page. Everything finer
///     lives in bUnit and the handler facts.
/// </summary>
[Collection("E2E")]
public sealed class ChartsSrpTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public ChartsSrpTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        await _fixture.Seed.SeedPhoenixChartAsync("Wire Shock", 20, "Double");
        await _fixture.Seed.SeedPhoenixChartAsync("Stub Groove", 20, "Double");
        await _fixture.Seed.SeedPhoenixChartAsync("Solo Circuit", 18, "Single");
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task AFilteredUrlLandsFilteredRefiltersInPlaceAndCardsLinkToTheChartPage()
    {
        // Landing from a shared link: the query string IS the filter state.
        await _page.GotoAsync("/Charts?LevelMin=20&LevelMax=20&Type=Double");
        var timeout = new LocatorAssertionsToHaveCountOptions { Timeout = 60_000 };
        await Expect(_page.Locator(".srp-card")).ToHaveCountAsync(2, timeout);

        // Live filtering: the drawer's song-name filter narrows without a reload and the
        // URL rewrites through history interop so the state stays shareable.
        await _page.Locator("button[aria-label=Filters]").ClickAsync();
        var songInput = _page.Locator(".srp-drawer input").First;
        await songInput.FillAsync("Wire");
        await songInput.BlurAsync();
        await Expect(_page.Locator(".srp-card")).ToHaveCountAsync(1, timeout);
        await Expect(_page).ToHaveURLAsync(new Regex("Song=Wire"),
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });
        await _page.Locator(".mud-overlay").ClickAsync();

        // The card is one link to the canonical chart page.
        await _page.Locator(".srp-card-link").First.ClickAsync();
        await Expect(_page).ToHaveURLAsync(new Regex("/Charts/phoenix/wire-shock/d20"),
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });
    }

    [Fact]
    public async Task AChartTheViewersMixDroppedStillRendersItsPage()
    {
        // The viewer sits in Phoenix; the chart is XX-only, reached by canonical URL, the
        // sitemap or a search result. The URL resolves, so the chart exists — the page must
        // render it from the mix that carries it rather than 404 (field-test round 1).
        await _fixture.Seed.SeedXXChartAsync("Legacy Relic", 19, "Double");

        await _page.GotoAsync("/Charts/xx/legacy-relic/d19");

        await Expect(_page.Locator("text=Legacy Relic").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await Expect(_page.Locator("text=404")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ALegacyCoOpChartPageRendersFromItsOwnMix()
    {
        // The exact field-test URL shape: /Charts/infinity/gargoyle-full-song/coop27 — a
        // co-op chart living only in a pumpout-era mix, whose difficulty slug carries a real
        // level rather than a player count.
        await _fixture.Seed.SeedLegacyChartAsync(E2ESeedData.InfinityMixId, "Infinity",
            "Gargoyle - FULL SONG -", 27, "CoOp");

        await _page.GotoAsync("/Charts/infinity/gargoyle-full-song/coop27");

        await Expect(_page.Locator("text=Gargoyle").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await Expect(_page.Locator("text=404 — PAGE NOT FOUND")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheOldPagesParameterNamesStillLandFiltered()
    {
        // Pre-redesign shared links keep working as read-time aliases.
        await _page.GotoAsync("/Charts?Difficulty=20&ChartType=Double");

        var timeout = new LocatorAssertionsToHaveCountOptions { Timeout = 60_000 };
        await Expect(_page.Locator(".srp-card")).ToHaveCountAsync(2, timeout);
    }

    [Fact]
    public async Task TheMoreFiltersListStaysPutWhenThePageBehindScrolls()
    {
        // Field-test round 4: the open pick list appeared to slide with the background. The
        // drawer is position:fixed but MudBlazor's popovers live in a provider at document
        // level, so an absolutely-positioned one travels with the document while its anchor
        // does not. Two guarantees, measured rather than eyeballed: while the list is open
        // the page behind cannot scroll at all, and the gap between list and input does not
        // move — through a page wheel and through the drawer's own content scrolling.
        // The page has to be long enough to actually scroll, or this proves nothing.
        for (var i = 0; i < 20; i++)
            await _fixture.Seed.SeedPhoenixChartAsync($"Filler {i:00}", 15, "Single");

        await _page.GotoAsync("/Charts", new PageGotoOptions { Timeout = 60_000 });
        await Expect(_page.Locator(".srp-card").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });

        await _page.Locator("button[aria-label=Filters]").ClickAsync();
        await _page.Locator(".srp-more-filters").ClickAsync();
        var popover = _page.Locator(".mud-popover-open").First;
        await Expect(popover).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

        var anchorBefore = await _page.Locator(".srp-more-filters").BoundingBoxAsync();
        var popoverBefore = await popover.BoundingBoxAsync();

        await _page.Mouse.WheelAsync(0, 600);
        await _page.WaitForTimeoutAsync(400);

        // The page behind is locked while the drawer is open, so a wheel cannot slide it out
        // from under the list — even though the results below are long enough to scroll.
        Assert.Equal(0, await _page.EvaluateAsync<double>("window.scrollY"));

        var anchorAfter = await _page.Locator(".srp-more-filters").BoundingBoxAsync();
        var popoverAfter = await popover.BoundingBoxAsync();

        var driftBefore = popoverBefore!.Y - anchorBefore!.Y;
        var driftAfter = popoverAfter!.Y - anchorAfter!.Y;
        Assert.True(Math.Abs(driftAfter - driftBefore) < 4,
            $"the list drifted {driftAfter - driftBefore:0.#}px from its input when the page scrolled");

        // And the other way it can happen: the drawer's own content scrolling under a
        // popover that is anchored to the document. A short viewport guarantees it scrolls.
        await _page.SetViewportSizeAsync(1280, 420);
        await _page.WaitForTimeoutAsync(300);
        var anchorTight = await _page.Locator(".srp-more-filters").BoundingBoxAsync();
        var popoverTight = await popover.BoundingBoxAsync();

        await _page.Mouse.MoveAsync(anchorTight!.X + 10, anchorTight.Y + 10);
        await _page.Mouse.WheelAsync(0, 300);
        await _page.WaitForTimeoutAsync(400);

        var anchorScrolled = await _page.Locator(".srp-more-filters").BoundingBoxAsync();
        var popoverScrolled = await popover.BoundingBoxAsync();
        var tightBefore = popoverTight!.Y - anchorTight.Y;
        var tightAfter = popoverScrolled!.Y - anchorScrolled!.Y;
        Assert.True(Math.Abs(tightAfter - tightBefore) < 4,
            $"the list drifted {tightAfter - tightBefore:0.#}px from its input when the drawer scrolled");
    }
}
