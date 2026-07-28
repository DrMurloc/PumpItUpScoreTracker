using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using Xunit.Abstractions;
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
    /// <summary>
    ///     The errors that mean the island/provider seam broke, as opposed to noise. A popover
    ///     that cannot find a provider says so by name; a faulted root and a dead circuit are
    ///     the two ways an island stops existing at all (same signatures ChartPageIslandTests
    ///     watches). Anything else the page throws is reported, not failed on: MudBlazor's own
    ///     interop is racy under load and a transient TypeError from inside it says nothing
    ///     about whether the search works — the popover opening is what says that.
    /// </summary>
    private static readonly string[] SeamFaults =
    {
        "MudPopoverProvider",
        "component operations",
        "Cannot send data"
    };

    private readonly E2EAppFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public StaticShellTests(E2EAppFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
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
    public async Task TheSearchIslandOpensItsPopover()
    {
        var watch = new PageErrorWatch(_page, _output);

        await _page.GotoAsync("/TierLists");

        // Scoped to the header: the same island is mounted twice now — app bar on desktop,
        // search sheet on mobile — so ".appbar-search" alone names two inputs. This one is
        // the desktop's; the sheet's is the phone's only door to a chart page.
        var search = _page.Locator("header .appbar-search input");
        await Expect(search).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });

        // Typed, not filled: MudAutocomplete opens off the keystrokes.
        await search.ClickAsync();
        await search.PressSequentiallyAsync("Conflict", new LocatorPressSequentiallyOptions { Delay = 60 });

        // The open popover is the assertion. A holder proves nothing — MudBlazor renders one
        // per popover regardless, and asking the service to open it is what throws when the
        // island can't reach a provider.
        await Expect(_page.Locator(".mud-popover-open").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
        await Expect(_page.Locator(".mud-popover-open").GetByText("Conflict").First).ToBeVisibleAsync();

        watch.AssertNoSeamFault();
    }

    /// <summary>
    ///     A phone's only door to /Chart/{id} — /Charts lists charts without linking to one, so
    ///     before this sheet existed there was no route at all. Three things have to hold at
    ///     once and only a hosted run can see any of them: nav.js opens a sheet it did not
    ///     render, an INTERACTIVE island renders inside STATIC shell chrome (the first place
    ///     the shell does this), and the field is focused by the opening click so the keyboard
    ///     comes up with it.
    /// </summary>
    [Fact]
    public async Task TheMobileSearchSheetOpensFocusedOnItsField()
    {
        var watch = new PageErrorWatch(_page, _output);

        // Below the shell's 960px breakpoint, where the bottom nav takes over from the top nav.
        await _page.SetViewportSizeAsync(390, 844);
        await _page.GotoAsync("/TierLists");

        var field = _page.Locator("[data-search-sheet] input");
        // The island fills static chrome, so it arrives with the circuit rather than the HTML.
        await Expect(field).ToBeAttachedAsync(new LocatorAssertionsToBeAttachedOptions { Timeout = 60_000 });

        await _page.Locator("[data-search-btn]").ClickAsync();

        await Expect(_page.Locator("[data-search-sheet]")).ToHaveClassAsync(new Regex(@"\bopen\b"));
        await Expect(field).ToBeFocusedAsync();
        watch.AssertNoSeamFault();
    }

    /// <summary>
    ///     Collects everything the page throws or logs as an error and writes all of it to the
    ///     test's output, pass or fail. These tests can only fail on a machine nobody is sitting
    ///     at, and asserting on a collection directly buys a message xUnit truncates to fifty
    ///     characters — too short to name what threw, let alone where.
    /// </summary>
    private sealed class PageErrorWatch
    {
        private readonly ConcurrentQueue<string> _seen = new();
        private readonly ITestOutputHelper _output;

        public PageErrorWatch(IPage page, ITestOutputHelper output)
        {
            _output = output;
            // Playwright raises these off the test's thread, so the sink has to be concurrent.
            page.PageError += (_, error) => _seen.Enqueue("PAGEERROR " + error);
            page.Console += (_, message) =>
            {
                if (message.Type == "error") _seen.Enqueue("CONSOLE " + message.Text);
            };
        }

        public void AssertNoSeamFault()
        {
            var seen = _seen.ToArray();
            foreach (var entry in seen) _output.WriteLine(entry);

            var faults = seen
                .Where(e => SeamFaults.Any(f => e.Contains(f, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            Assert.True(faults.Length == 0,
                "The island could not reach its Mud providers:\n\n"
                + string.Join("\n\n", faults)
                + "\n\nEverything the page reported:\n"
                + string.Join("\n", seen));
        }
    }
}
