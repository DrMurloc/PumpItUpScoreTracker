using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The home dashboard's critical workflow: create the curated default and reorder by
///     drag. Drag is exactly the regression class unit tests can't see (pointer geometry +
///     JS interop + persistence round-trip), which is why it earns an E2E fact. The seeded
///     chart matters — the harness runs with DevAuth on, which arms the empty-database
///     guard that would otherwise bounce the dashboard to /Dev/Populate.
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
        await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 20, "Single");
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
    public async Task CreatesTheCuratedDefaultAndDragReordersPersistently()
    {
        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        // Signed-in "/" is the dashboard now (the dispatcher boots the app here).
        await _page.GotoAsync("/");

        // First visit → create the curated default: eight widgets, pre-placed.
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create" })
            .ClickAsync(new LocatorClickOptions { Timeout = 60_000 });
        var titles = _page.Locator(".dash-cell .dash-widget-title");
        await Expect(titles.First).ToBeVisibleAsync(timeout);
        await Expect(titles).ToHaveCountAsync(8);

        // Title-agnostic so a widget rename never breaks the drag regression.
        var first = (await titles.Nth(0).TextContentAsync() ?? string.Empty).Trim();
        var second = (await titles.Nth(1).TextContentAsync() ?? string.Empty).Trim();

        // Edit mode: drag the first widget's handle onto the far side of the second.
        await _page.Locator("button[title='Edit']").ClickAsync(new LocatorClickOptions { Timeout = 60_000 });
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

        // Swap semantics: 0 and 1 trade places, bystanders stay put.
        await Expect(titles.Nth(0)).ToHaveTextAsync(second,
            new LocatorAssertionsToHaveTextOptions { Timeout = 60_000 });
        await Expect(titles.Nth(1)).ToHaveTextAsync(first);

        // The drop dispatched one MoveHomePageWidgetCommand — the order survives a reload.
        await _page.ReloadAsync();
        await Expect(titles.First).ToBeVisibleAsync(timeout);
        await Expect(titles.Nth(0)).ToHaveTextAsync(second);
        await Expect(titles.Nth(1)).ToHaveTextAsync(first);
    }
}
