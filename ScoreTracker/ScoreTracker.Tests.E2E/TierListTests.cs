using System.Text.RegularExpressions;
using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

[Collection("E2E")]
public sealed class TierListTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public TierListTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();

        // Four Double 20 charts spread across Pass Count categories — the default
        // "Pass Difficulty" lens renders from the Pass Count list, so /TierLists/Double/20
        // makes exactly these sections appear (Overrated renders under its player-facing
        // name, "1+ Level Easier"). One Single 18 chart backs the folder-picker hop.
        var easy1 = await _fixture.Seed.SeedPhoenixChartAsync("E2E Anthem", 20, "Double");
        var easy2 = await _fixture.Seed.SeedPhoenixChartAsync("Stub Groove", 20, "Double");
        var hard = await _fixture.Seed.SeedPhoenixChartAsync("Mock Parade", 20, "Double");
        var overrated = await _fixture.Seed.SeedPhoenixChartAsync("Wire Shock", 20, "Double");
        var single = await _fixture.Seed.SeedPhoenixChartAsync("Solo Circuit", 18, "Single");
        await _fixture.Seed.SeedTierListEntryAsync("Pass Count", easy1, "Easy", 0);
        await _fixture.Seed.SeedTierListEntryAsync("Pass Count", easy2, "Easy", 1);
        await _fixture.Seed.SeedTierListEntryAsync("Pass Count", hard, "Hard", 0);
        await _fixture.Seed.SeedTierListEntryAsync("Pass Count", overrated, "Overrated", 0);
        await _fixture.Seed.SeedTierListEntryAsync("Pass Count", single, "Medium", 0);
        // The details dialog leads with the video (C6) — give every folder chart one.
        foreach (var chartId in new[] { easy1, easy2, hard, overrated })
            await _fixture.Seed.SeedChartVideoAsync(chartId, "https://e2e-files.invalid/video");

        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task FolderRouteRendersTheSeededTierSectionsWithChartCards()
    {
        await _page.GotoAsync("/TierLists/Double/20");

        // Sections appear only for categories that have charts (empty ones are hidden).
        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        await Expect(SectionNamed("Easy")).ToBeVisibleAsync(timeout);
        await Expect(SectionNamed("Hard")).ToBeVisibleAsync();
        await Expect(SectionNamed("1+ Level Easier")).ToBeVisibleAsync();

        // No Medium chart lives in this folder, so that section must not render.
        await Expect(SectionNamed("Medium")).ToHaveCountAsync(0);

        // Each seeded chart renders as a chart card with its jacket as the identifier.
        var cards = _page.Locator(".tier-chart-card");
        var cardCount = await cards.CountAsync();
        Assert.True(cardCount >= 4, $"Expected at least the 4 seeded chart cards, found {cardCount}.");
        var jackets = _page.Locator(".tier-chart-card-jacket[style*='e2e-files.invalid/songs/']");
        Assert.True(await jackets.CountAsync() >= 4, "Chart cards did not render the seeded song jackets.");
    }

    [Fact]
    public async Task LegacyUrlsRedirectToTheCanonicalFolderRoute()
    {
        // The C3 301 middleware: the query-param form becomes the path route...
        await _page.GotoAsync("/TierLists?Difficulty=20&ChartType=Double");
        await Expect(_page).ToHaveURLAsync(new Regex("/TierLists/Double/20"),
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });

        // ...and the retired page names land on /TierLists.
        await _page.GotoAsync("/ChartSkills");
        await Expect(_page).ToHaveURLAsync(new Regex("/TierLists"),
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });

        // The deleted old page's URL survives as a 301 for stray bookmarks (C14).
        await _page.GotoAsync("/TierLists/Old");
        await Expect(_page).ToHaveURLAsync(new Regex("/TierLists/Double/18"),
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });
    }

    [Fact]
    public async Task BareTierListsCanonicalizesInPlaceWithoutReloading()
    {
        // The bare route resolves a folder at the end of its init and rewrites the URL bar
        // via history.replaceState. Navigating there instead would re-fetch and re-initialize
        // the page it just rendered, stack junk history entries, and knock down any shell
        // sheet the user had opened while init was still running — so the pins are on all
        // three: one load total, an open sheet that survives the rewrite, and Back leaving
        // the page rather than bouncing forward.
        var tierListLoads = new List<string>();
        _page.Request += (_, r) =>
        {
            if (r.Url.Contains("/TierLists", StringComparison.OrdinalIgnoreCase)
                && r.ResourceType is "document" or "fetch")
                lock (tierListLoads) tierListLoads.Add($"{r.ResourceType} {r.Url}");
        };

        // Below the shell's 960px breakpoint so the bottom nav (and its Search slot) exists.
        await _page.SetViewportSizeAsync(390, 844);
        await _page.GotoAsync("/LifeCalculator");
        await _page.GotoAsync("/TierLists");

        // Opened straight off the load: the sheet is static chrome, so this lands before the
        // page's init resolves a folder — the tap the CI race caught being swallowed.
        await _page.Locator("[data-search-btn]").ClickAsync();
        await Expect(_page.Locator("[data-search-sheet]")).ToHaveClassAsync(new Regex(@"\bopen\b"));

        await Expect(_page).ToHaveURLAsync(new Regex(@"/TierLists/\w+/\d+\?TierListType="),
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });

        // The rewrite lands at the end of init, so by now any re-fetch it triggered would be
        // in flight; a beat of settle catches it before the count is pinned.
        await _page.WaitForTimeoutAsync(1000);
        lock (tierListLoads)
        {
            Assert.True(tierListLoads.Count == 1,
                $"Expected the single document load, saw: {string.Join(", ", tierListLoads)}");
        }
        await Expect(_page.Locator("[data-search-sheet]")).ToHaveClassAsync(new Regex(@"\bopen\b"));

        await _page.GoBackAsync();
        await Expect(_page).ToHaveURLAsync(new Regex("/LifeCalculator"),
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });
    }

    [Fact]
    public async Task FolderPickerSwitchesTypeAndLevelInOneGesture()
    {
        // The headline perf fix of the overhaul: D20 → S18 is one popover interaction,
        // not two sequential page loads.
        await _page.GotoAsync("/TierLists/Double/20");
        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        await Expect(SectionNamed("Easy")).ToBeVisibleAsync(timeout);

        await Toolbar().GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "D20" }).ClickAsync();
        var popover = _page.Locator(".folder-picker-pop");
        await Expect(popover).ToBeVisibleAsync();
        // Switching the chart type must NOT close the panel (the round-6 picker fix).
        await popover.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Singles", Exact = true }).ClickAsync();
        await Expect(popover).ToBeVisibleAsync();
        await popover.Locator(".folder-picker-level")
            .Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^18$") }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex("/TierLists/Single/18"),
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });
        await Expect(SectionNamed("Medium")).ToBeVisibleAsync(timeout);
    }

    [Fact]
    public async Task ChartCardOpensTheDetailsDialogVideoFirst()
    {
        await _page.GotoAsync("/TierLists/Double/20");
        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        await Expect(SectionNamed("Easy")).ToBeVisibleAsync(timeout);

        await _page.Locator(".tier-chart-card-jacket").First.ClickAsync();

        var dialog = _page.Locator(".mud-dialog");
        await Expect(dialog).ToBeVisibleAsync(timeout);
        // C6: the dialog leads with the video.
        await Expect(dialog.Locator("iframe.chart-details-video")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task DownloadProducesTheShareCardPng()
    {
        // Exercises the whole share pipeline — GetTierListShareCardQuery through the
        // SkiaSharp renderer — and is the only pre-production check of the SkiaSharp
        // Linux native assets in CI.
        await _page.GotoAsync("/TierLists/Double/20");
        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        await Expect(SectionNamed("Easy")).ToBeVisibleAsync(timeout);

        // Round 7 moved Download to the sticky toolbar (right-aligned action slot).
        var download = await _page.RunAndWaitForDownloadAsync(
            () => _page.Locator(".tier-toolbar")
                .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Download" }).ClickAsync(),
            new PageRunAndWaitForDownloadOptions { Timeout = 60_000 });

        // The mix is part of the filename now — a shared "Double 20" card is
        // meaningless without it (E2E runs on the default Phoenix mix).
        Assert.StartsWith("TierList_Phoenix_Double20_", download.SuggestedFilename);
        Assert.EndsWith(".png", download.SuggestedFilename);
        var path = await download.PathAsync();
        var bytes = await File.ReadAllBytesAsync(path!);
        Assert.True(bytes.Length > 8, "Downloaded share card was empty.");
        // PNG magic number — the renderer produced a real image, not an error payload.
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
    }

    private ILocator Toolbar()
    {
        return _page.Locator(".tier-toolbar");
    }

    private ILocator SectionNamed(string name)
    {
        return _page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = name, Exact = true });
    }
}
