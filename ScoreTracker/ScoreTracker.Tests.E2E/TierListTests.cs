using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

[Collection("E2E")]
public sealed class TierListTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public TierListTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();

        // Four Double 20 charts spread across Pass Count categories. The default tier list
        // ("Stage Break") renders from the Pass Count entries, so pinning
        // ?Difficulty=20&ChartType=Double makes exactly these sections appear.
        var easy1 = await _fixture.Seed.SeedPhoenixChartAsync("E2E Anthem", 20, "Double");
        var easy2 = await _fixture.Seed.SeedPhoenixChartAsync("Stub Groove", 20, "Double");
        var hard = await _fixture.Seed.SeedPhoenixChartAsync("Mock Parade", 20, "Double");
        var overrated = await _fixture.Seed.SeedPhoenixChartAsync("Wire Shock", 20, "Double");
        await _fixture.Seed.SeedTierListEntryAsync("Pass Count", easy1, "Easy", 0);
        await _fixture.Seed.SeedTierListEntryAsync("Pass Count", easy2, "Easy", 1);
        await _fixture.Seed.SeedTierListEntryAsync("Pass Count", hard, "Hard", 0);
        await _fixture.Seed.SeedTierListEntryAsync("Pass Count", overrated, "Overrated", 0);

        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task TierListsPageRendersTheSeededSectionsWithChartCards()
    {
        await _page.GotoAsync("/TierLists?Difficulty=20&ChartType=Double");

        // Sections appear only for categories that have charts (empty ones are hidden).
        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        await Expect(HeadingNamed("Easy")).ToBeVisibleAsync(timeout);
        await Expect(HeadingNamed("Hard")).ToBeVisibleAsync();
        await Expect(HeadingNamed("Overrated")).ToBeVisibleAsync();

        // No Medium entries were seeded, so that section must not render.
        await Expect(HeadingNamed("Medium")).ToHaveCountAsync(0);

        // Each seeded chart renders as a chart card with its song image as the header background.
        var cards = _page.Locator(".mud-card.chart-card");
        var cardCount = await cards.CountAsync();
        Assert.True(cardCount >= 4, $"Expected at least the 4 seeded chart cards, found {cardCount}.");
        var seededImages = _page.Locator(".mud-card.chart-card .mud-card-header[style*='e2e-files.invalid/songs/']");
        Assert.True(await seededImages.CountAsync() >= 4, "Chart cards did not render the seeded song images.");
    }

    private ILocator HeadingNamed(string name)
    {
        return _page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = name, Exact = true });
    }
}
