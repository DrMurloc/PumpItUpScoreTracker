using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     A song's channel reaches the two surfaces a player reads it on: the static chart page's
///     fact row and the details dialog's Chart Stats rows (docs/design/song-channels.md §5, D10).
///     One seeded row, the real app on Kestrel, a real browser — the whole path from the SongMix
///     table through the per-mix chart dictionary to the markup.
/// </summary>
[Collection("E2E")]
public sealed class SongChannelTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public SongChannelTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var chartId = await _fixture.Seed.SeedPhoenixChartAsync("Nostalgia", 21, "Double");
        await _fixture.Seed.SeedSongChannelAsync(chartId, E2ESeedData.PhoenixMixId, "KPop");
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task TheChartPageShowsTheChannelFact()
    {
        await _page.GotoAsync("/Charts/phoenix/nostalgia/d21");

        var label = _page.Locator(".chart-fact-label", new PageLocatorOptions { HasTextString = "Channel" });
        await Expect(label).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
        await Expect(_page.Locator(".chart-fact-value", new PageLocatorOptions { HasTextString = "K-Pop" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task TheDetailsDialogShowsTheChannelRowOnChartStats()
    {
        await _page.GotoAsync("/Charts");
        await Expect(_page.Locator(".srp-card")).ToHaveCountAsync(1,
            new LocatorAssertionsToHaveCountOptions { Timeout = 30_000 });

        await _page.Locator(".srp-card-link").First.ClickAsync();
        await Expect(_page.Locator(".mud-dialog")).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
        // The identity line reads under the title before any tab is picked.
        await Expect(_page.Locator(".chart-details-sub")).ToContainTextAsync("song by");
        await _page.Locator("[data-testid='cdt-tab-Stats']").ClickAsync();

        await Expect(_page.Locator(".chart-details-row-channel"))
            .ToContainTextAsync("K-Pop", new LocatorAssertionsToContainTextOptions { Timeout = 30_000 });
        // A seeded debut with no rerate: the History row is the one event the chart's own row can say.
        await Expect(_page.Locator(".chart-details-row-history")).ToContainTextAsync("Debuted");
        await Expect(_page.Locator(".chart-details-row-history")).ToContainTextAsync("D21");
    }
}
