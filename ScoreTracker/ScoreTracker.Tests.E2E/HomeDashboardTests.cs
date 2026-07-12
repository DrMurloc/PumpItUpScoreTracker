using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The home dashboard's critical workflow: build a page in edit mode and reorder
///     by drag. Drag is exactly the regression class unit tests can't see (pointer
///     geometry + JS interop + persistence round-trip), which is why it earns an E2E
///     fact (design doc §2.4 / C9).
/// </summary>
[Collection("E2E")]
public sealed class HomeDashboardTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;
    private Guid _user;

    public HomeDashboardTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _user = await _fixture.Seed.SeedUserAsync("DashBuilder");
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();

        // DevAuth backdoor (fixture enables DevAuth): the sign-in cookie lands in this
        // browser context via an in-page form post.
        await _page.GotoAsync("/Login");
        await _page.EvaluateAsync(
            "id => fetch('/Login/Dev', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'userId=' + id })",
            _user.ToString());
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task BuildsAPageAndDragReordersWidgetsPersistently()
    {
        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        await _page.GotoAsync("/Home");

        // First-visit hero → create the first page; the beta starter pre-places the trio.
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create" })
            .ClickAsync(new LocatorClickOptions { Timeout = 60_000 });
        var titles = _page.Locator(".dash-cell .dash-widget-title");
        await Expect(titles.First).ToBeVisibleAsync(timeout);
        await Expect(titles).ToHaveCountAsync(3);
        await Expect(titles.Nth(0)).ToHaveTextAsync("Competitive Level");

        // Edit mode: the drawer adds a SECOND Pumbility — multiple instances of one
        // type are supported by design (per-instance config).
        await _page.Locator("button[title='Edit']").ClickAsync(new LocatorClickOptions { Timeout = 60_000 });
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add Widgets" }).First
            .ClickAsync();
        await _page.Locator(".dash-drawer-item", new PageLocatorOptions { HasTextString = "PUMBILITY" })
            .ClickAsync(new LocatorClickOptions { Timeout = 60_000 });
        // The temporary drawer closes via its overlay — Escape isn't bound.
        await _page.Locator(".mud-overlay").ClickAsync();
        await Expect(_page.Locator(".mud-overlay")).ToBeHiddenAsync();
        await Expect(titles).ToHaveCountAsync(4);
        await Expect(titles.Nth(3)).ToHaveTextAsync("PUMBILITY");

        // Drag the first widget's handle onto the far side of the second widget.
        var handle = _page.Locator(".dash-cell").Nth(0).Locator(".dash-drag-handle");
        var handleBox = await handle.BoundingBoxAsync()
                        ?? throw new InvalidOperationException("Drag handle not visible");
        var targetBox = await _page.Locator(".dash-cell").Nth(1).BoundingBoxAsync()
                        ?? throw new InvalidOperationException("Target cell not visible");
        await _page.Mouse.MoveAsync((float)(handleBox.X + handleBox.Width / 2),
            (float)(handleBox.Y + handleBox.Height / 2));
        await _page.Mouse.DownAsync();
        await _page.Mouse.MoveAsync((float)(targetBox.X + targetBox.Width * 0.9),
            (float)(targetBox.Y + targetBox.Height / 2), new MouseMoveOptions { Steps = 12 });
        await _page.Mouse.UpAsync();

        await Expect(titles.Nth(0)).ToHaveTextAsync("PUMBILITY",
            new LocatorAssertionsToHaveTextOptions { Timeout = 60_000 });
        await Expect(titles.Nth(1)).ToHaveTextAsync("Competitive Level");
        // Swap semantics: the bystander widgets don't move (the round-1 bug's pin).
        await Expect(titles.Nth(2)).ToHaveTextAsync("Weekly Charts");

        // The drop dispatched one MoveHomePageWidgetCommand — the order survives a reload.
        await _page.ReloadAsync();
        await Expect(titles.First).ToBeVisibleAsync(timeout);
        await Expect(titles.Nth(0)).ToHaveTextAsync("PUMBILITY");
        await Expect(titles.Nth(1)).ToHaveTextAsync("Competitive Level");
    }
}
