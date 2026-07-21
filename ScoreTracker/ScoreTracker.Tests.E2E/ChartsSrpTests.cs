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
        // Reached from All Mixes: the viewer sits in Phoenix, the chart is XX-only. The URL
        // resolves, so the chart exists — the page must render it from the mix that carries
        // it rather than 404 (field-test round 1).
        await _fixture.Seed.SeedXXChartAsync("Legacy Relic", 19, "Double");

        await _page.GotoAsync("/Charts/xx/legacy-relic/d19");

        await Expect(_page.Locator("text=Legacy Relic").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await Expect(_page.Locator("text=404")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TheOldPagesParameterNamesStillLandFiltered()
    {
        // Pre-redesign shared links keep working as read-time aliases.
        await _page.GotoAsync("/Charts?Difficulty=20&ChartType=Double");

        var timeout = new LocatorAssertionsToHaveCountOptions { Timeout = 60_000 };
        await Expect(_page.Locator(".srp-card")).ToHaveCountAsync(2, timeout);
    }
}
