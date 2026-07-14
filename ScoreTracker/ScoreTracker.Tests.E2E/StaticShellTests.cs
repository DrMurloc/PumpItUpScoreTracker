using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The static shell (docs/design/static-shell.md): the nav, the theme and the title are
///     server-rendered HTML on every page, and the app boots into the content region under
///     them. These are facts only a hosted run can see — the shell's whole point is what the
///     response contains before a circuit exists, and the search island's whole risk is that
///     it renders from a different root than the Mud providers it depends on.
/// </summary>
[Collection("E2E")]
public sealed class StaticShellTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public StaticShellTests(E2EAppFixture fixture)
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

    /// <summary>
    ///     The response carries the nav before any JS runs. This is what a crawler and a link
    ///     unfurler see, and it is the reason the shell exists — so it is asserted against the
    ///     raw body, not the rendered DOM.
    /// </summary>
    [Fact]
    public async Task TheNavAndTitleAreInTheHtmlBeforeAnyCircuit()
    {
        var response = await _page.APIRequest.GetAsync($"{_fixture.BaseUrl}/TierLists");
        var html = await response.TextAsync();

        Assert.Contains("shell-appbar", html);
        Assert.Contains("bottom-nav", html);
        Assert.Contains("<title>", html);
        // Every --mud-* custom property is emitted by MudThemeProvider inside the circuit, so
        // a shell that reaches for one paints unthemed until the circuit arrives.
        Assert.DoesNotContain("var(--mud-", html);
    }

    /// <summary>
    ///     The app-bar search is its own root component, mounted from the layout — but the
    ///     MudPopoverProvider its autocomplete depends on lives in MainLayout, inside the app's
    ///     root. Two roots, one circuit, one scoped popover service. This asserts that seam:
    ///     the island renders, and the provider in the other root picks up the popover it
    ///     registers. If it ever breaks, the input still renders and simply never opens —
    ///     which nothing else would catch.
    /// </summary>
    [Fact]
    public async Task TheSearchIslandBootsAndItsPopoverRegistersWithTheLayoutsProvider()
    {
        await _page.GotoAsync("/TierLists");

        // The input only exists if the island's own root booted on the shared circuit.
        await Expect(_page.Locator(".appbar-search input"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });

        // ...and this holder is rendered by MainLayout's provider, in the other root, for a
        // popover the island registered. Its presence is the cross-root proof; whether it is
        // open depends on what the user typed, which is the autocomplete's business, not the
        // shell's.
        await Expect(_page.Locator("[id^='popovercontent-']").First)
            .ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = 30_000 });
    }
}
