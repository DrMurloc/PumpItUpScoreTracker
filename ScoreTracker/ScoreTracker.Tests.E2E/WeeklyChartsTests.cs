using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The challenges hub (docs/design/weekly-charts-overhaul.md): a static-SSR page whose whole
///     point is what the response contains before a circuit exists — the week's charts, a real
///     title, the JSON-LD — and whose one island (the dialog host) has to wire up through
///     challenge-board.js and render its dialogs from the hoisted Mud providers. Both are facts
///     only a hosted run can see.
/// </summary>
[Collection("E2E")]
public sealed class WeeklyChartsTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;
    private Guid _chartId;
    private Guid _userId;

    public WeeklyChartsTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _chartId = await _fixture.Seed.SeedPhoenixChartAsync("Napalm", 22, "Single");
        _userId = await _fixture.Seed.SeedUserAsync("E2EPLAYER");
        // A live board that won't expire during the run, with one rival score so a name shows.
        await _fixture.Seed.SeedWeeklyChartAsync(_chartId, DateTimeOffset.UtcNow.AddDays(3));
        var rival = await _fixture.Seed.SeedUserAsync("RIVAL");
        await _fixture.Seed.SeedWeeklyEntryAsync(rival, _chartId, 990_000);
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    /// <summary>
    ///     The payoff of the static rebuild: an anonymous request carries the week's charts, a
    ///     real title, the concept copy and the JSON-LD — all before any JS. Asserted against the
    ///     raw body, since that is exactly what a crawler and a link unfurler receive.
    /// </summary>
    [Fact]
    public async Task TheWeekAndItsSeoAreInTheHtmlBeforeAnyCircuit()
    {
        var response = await _page.APIRequest.GetAsync($"{_fixture.BaseUrl}/WeeklyCharts");
        var html = await response.TextAsync();

        Assert.Contains("Napalm", html);                            // the week's chart, server-rendered
        Assert.Contains("<title>", html);
        Assert.Contains("application/ld+json", html);               // the ItemList
        Assert.Contains("og:description", html);                    // the unfurl card
        Assert.Contains("PUMBILITY", html);                         // the concept copy in the description
        // A static region is --mix-*-only; a --mud-* var would paint unthemed until the circuit.
        Assert.DoesNotContain("var(--mud-", html);
    }

    /// <summary>
    ///     The island seam: a click on the static Record control has to reach the dialog host
    ///     through challenge-board.js, and the host's MudDialog has to render from the providers
    ///     hoisted ahead of it. Recording then reloads the static page with the new standing —
    ///     the round-trip end to end.
    /// </summary>
    [Fact]
    public async Task RecordingThroughTheIslandLandsOnTheBoard()
    {
        // DevAuth backdoor (the fixture enables it): drop the sign-in cookie into this context.
        await _page.GotoAsync("/Login");
        await _page.EvaluateAsync(
            "id => fetch('/Login/Dev', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'userId=' + id })",
            _userId.ToString());

        await _page.GotoAsync("/WeeklyCharts");

        // The Record control is static HTML; clicking it must open the island's dialog — which
        // only happens once the circuit has connected and registered with challenge-board.js.
        // The button is visible before that, so wait for the island's own ready signal (else the
        // click races registration and is silently inert).
        var record = _page.Locator("[data-challenge-record]").First;
        await Expect(record).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await _page.WaitForFunctionAsync("() => document.documentElement.hasAttribute('data-challenge-ready')",
            null, new PageWaitForFunctionOptions { Timeout = 60_000 });
        await record.ClickAsync();

        var dialog = _page.Locator(".mud-dialog");
        await Expect(dialog).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

        await dialog.Locator("input").First.FillAsync("987654");
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Submit" }).ClickAsync();

        // Submit reloads the static page; the caller's own row now sits on the card.
        await Expect(_page.Locator(".challenge-card-line.mine").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await Expect(_page.Locator(".challenge-card-line.mine").First).ToContainTextAsync("987,654");
    }
}
