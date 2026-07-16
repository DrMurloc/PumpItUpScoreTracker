using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The contracts MainLayout and the mix seed own, pinned so the per-page render-mode
///     flip has to keep them true: the page dock registers and reaches the shell, a page
///     drawer opens against its drawer container, the legacy-mix gate lands on the tier
///     lists, and an anonymous visitor's mix cookie reaches page content — not just the
///     shell. Every fact is written against behavior, never implementation, so they must
///     pass identically on both sides of the flip.
/// </summary>
[Collection("E2E")]
public sealed class LayoutContractTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public LayoutContractTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 20, "Single");
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    private Task SetMixCookieAsync(string mix)
    {
        return _browser.AddCookiesAsync(new[]
        {
            new Cookie { Name = "CurrentMix", Value = mix, Url = _fixture.BaseUrl }
        });
    }

    /// <summary>
    ///     The randomizer registers its dock on load: the shell learns about it on
    ///     &lt;html&gt; (shell.setDockState) and the layout's slot renders the content —
    ///     mobile-only via CSS, but present in the DOM at any width.
    /// </summary>
    [Fact]
    public async Task TheRandomizerDockRegistersAndReachesTheShell()
    {
        await _page.GotoAsync("/ChartRandomizer");

        await Expect(_page.Locator("html.has-dock"))
            .ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = 60_000 });
        await Expect(_page.Locator(".page-dock")).ToHaveCountAsync(1);
    }

    /// <summary>
    ///     A MudDrawer falls into normal flow without a drawer container to anchor to —
    ///     exactly the piece the flip relocates from the layout into the drawer pages.
    /// </summary>
    [Fact]
    public async Task TheRandomizerSettingsDrawerOpens()
    {
        await _page.GotoAsync("/ChartRandomizer");

        await _page.GetByText("Edit settings")
            .ClickAsync(new LocatorClickOptions { Timeout = 60_000 });

        await Expect(_page.Locator(".mud-drawer--open"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
    }

    /// <summary>
    ///     Gated legacy mixes never begin loading pages that predate them — they land on
    ///     the tier lists, the one destination every mix renders.
    /// </summary>
    [Fact]
    public async Task TheLegacyMixGateLandsOnTierLists()
    {
        await SetMixCookieAsync("Prime");

        await _page.GotoAsync("/LifeCalculator");

        await Expect(_page).ToHaveURLAsync($"{_fixture.BaseUrl}/TierLists",
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });
    }

    /// <summary>
    ///     An anonymous visitor's mix cookie must reach the circuit that renders page
    ///     content, not just the server-rendered shell: under XX the chart list shows the
    ///     XX catalog. If the request-resolved mix ever stops crossing into the circuit,
    ///     this renders Phoenix instead.
    /// </summary>
    [Fact]
    public async Task AnAnonymousMixCookieReachesPageContent()
    {
        await _fixture.Seed.SeedXXChartAsync("Love is a Danger Zone", 16, "Single");
        await SetMixCookieAsync("XX");

        await _page.GotoAsync("/Charts");

        // The chart row's identity is the jacket image; its alt text is the song name
        // (the Song Name text column is toggled off by default).
        await Expect(_page.GetByAltText("Love is a Danger Zone").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await Expect(_page.GetByAltText("Conflict")).ToHaveCountAsync(0);
    }
}
