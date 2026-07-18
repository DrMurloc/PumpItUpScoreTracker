using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The chart page is static HTML with interactive islands — and nothing verified the
///     islands actually connect until a real browser loaded one. The cutover facts use a
///     plain HttpClient and never boot a circuit, so this whole class of failure shipped
///     green: an island that takes a non-serializable parameter (a Chart record, a
///     polymorphic verdict list) faults the circuit with "the list of component operations
///     is not valid", and every island on the page dies while the static HTML looks fine.
///     The video jacket renders only once its island is live (prerender is off), so its
///     presence proves the circuit came up — and the console must be free of that fault.
/// </summary>
[Collection("E2E")]
public sealed class ChartPageIslandTests : IAsyncLifetime
{
    private const string Canonical = "/Charts/phoenix/conflict/s20";

    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;
    private readonly List<string> _consoleErrors = new();

    public ChartPageIslandTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 20, "Single");
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
        _page.Console += (_, msg) =>
        {
            // The seeded chart's jacket points at a fake host, so its image 404 is expected
            // noise; a component-operations fault is the failure this class exists to catch.
            if (msg.Type == "error" && !msg.Text.Contains("ERR_NAME_NOT_RESOLVED"))
                _consoleErrors.Add(msg.Text);
        };
        _page.PageError += (_, err) => _consoleErrors.Add("PAGEERROR " + err);
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task TheIslandsConnectAndRenderOnTheStaticPage()
    {
        await _page.GotoAsync(Canonical);

        // The jacket lives inside the video island; if the circuit came up, it appears.
        await Expect(_page.Locator(".chart-jacket"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

        Assert.DoesNotContain(_consoleErrors,
            e => e.Contains("component operations") || e.Contains("Cannot send data"));
    }
}
