using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The "/" dispatcher: one route, two worlds. Anonymous visitors get the circuit-free
///     front door (a real Razor Page — no Blazor, so crawlers and unfurlers see real HTML);
///     signed-in visitors get the Blazor app, whose router resolves "/" to the home
///     dashboard. "/Login" mirrors it, and bounces a signed-in visitor home. This is pure
///     routing/render behavior across the Razor-Pages/Blazor seam — the kind of thing only
///     a hosted end-to-end run can see. A chart is seeded because DevAuth is on in the
///     harness, arming the empty-database guard on both surfaces.
/// </summary>
[Collection("E2E")]
public sealed class FrontDoorDispatcherTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;
    private Guid _user;

    public FrontDoorDispatcherTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 20, "Single");
        _user = await _fixture.Seed.SeedUserAsync("Router");
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    private async Task SignInAsync()
    {
        await _page.GotoAsync("/Login");
        await _page.EvaluateAsync(
            "id => fetch('/Login/Dev', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'userId=' + id })",
            _user.ToString());
    }

    [Fact]
    public async Task AnonymousRootServesTheCircuitFreeFrontDoor()
    {
        await _page.GotoAsync("/");
        await Expect(_page.Locator("body.front-door"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await Expect(_page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Sign in" }))
            .ToBeVisibleAsync();
        // No Blazor host at all: the front door boots no circuit, which is the entire reason
        // it is a Razor Page. Asserted against the script the app cannot run without — the
        // MudBlazor wrapper this used to check is the app's to keep or drop, so it would go
        // quiet the day the app stopped rendering one.
        await Expect(_page.Locator("script[src*='blazor.server.js']")).ToHaveCountAsync(0);
        await Expect(_page.Locator("#blazor-error-ui")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AnonymousLoginRouteAlsoServesTheFrontDoor()
    {
        await _page.GotoAsync("/Login");
        await Expect(_page.Locator("body.front-door"))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
    }

    [Fact]
    public async Task SignedInRootServesTheHomeDashboard()
    {
        await SignInAsync();
        await _page.GotoAsync("/");
        // The app booted and the router resolved "/" to the dashboard's create hero.
        await Expect(_page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create" }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await Expect(_page.Locator("body.front-door")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SignedInLoginRouteRedirectsHome()
    {
        await SignInAsync();
        await _page.GotoAsync("/Login");
        await Expect(_page).ToHaveURLAsync($"{_fixture.BaseUrl}/",
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });
    }
}
