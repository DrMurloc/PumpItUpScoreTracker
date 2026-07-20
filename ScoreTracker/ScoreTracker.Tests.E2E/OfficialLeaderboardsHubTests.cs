using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The Official Leaderboards section end to end: a sealed snapshot in real SQL renders
///     through the real Kestrel app — the weekly world-first on the This Week front page,
///     and the computed rankings on their own routed page. The sweep that produces
///     snapshots is pinned by unit and integration tests; this covers the whole stack.
/// </summary>
[Collection("E2E")]
public sealed class OfficialLeaderboardsHubTests : IAsyncLifetime
{
    private static readonly DateTimeOffset SealedAt = DateTimeOffset.UtcNow.AddHours(-2);
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;

    public OfficialLeaderboardsHubTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var chartId = await _fixture.Seed.SeedPhoenixChartAsync("Wire Inferno", 26, "Double");
        await _fixture.Seed.SeedOfficialSnapshotAsync(chartId, "Wire Inferno D26", SealedAt);
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task ThisWeekRendersTheSeededWorldFirstFromTheSealedSnapshot()
    {
        await _page.GotoAsync("/OfficialLeaderboards");

        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        // The world-first row: PG chip, the chart's song, and the champion's tag.
        await Expect(_page.GetByText("E2ECHAMP").First).ToBeVisibleAsync(timeout);
        await Expect(_page.GetByText("Wire Inferno").First).ToBeVisibleAsync();
        await Expect(_page.Locator(".mud-chip", new PageLocatorOptions { HasTextString = "PG" }).First)
            .ToBeVisibleAsync();
        // The subtitle reads the sealed run's timestamp, not a stored value.
        await Expect(_page.GetByText("Last Updated")).ToBeVisibleAsync();
        // The section nav links every page in the group. Scoped to the page's nav —
        // the shell mega-menu also carries these labels, hidden until hover.
        await Expect(_page.Locator(".olb-section-nav").GetByText("What It Takes"))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task RankingsDeepLinkOpensTheComputedBoardWithTheSeededPlayers()
    {
        await _page.GotoAsync("/OfficialLeaderboards/Rankings");

        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        // Phoenix has no PUMBILITY board — the caption says computed, and both seeded
        // players rank by their computed rating (the million-point PG on top).
        await Expect(_page.GetByText("computed rating", new PageGetByTextOptions { Exact = false }))
            .ToBeVisibleAsync(timeout);
        await Expect(_page.GetByText("E2ECHAMP").First).ToBeVisibleAsync();
        await Expect(_page.GetByText("E2ERUNNER").First).ToBeVisibleAsync();
        // The board is compact rows, not a table: the rankings board is the leaderboard
        // golden standard (UX rule 5), so a row is .olb-rank-card and never a <tr>. The
        // count is exact — one row per player, so a re-introduced twin fails here.
        var rows = _page.Locator(".olb-rank-card", new PageLocatorOptions { HasTextString = "E2E" });
        await Expect(rows).ToHaveCountAsync(2);
    }
}
