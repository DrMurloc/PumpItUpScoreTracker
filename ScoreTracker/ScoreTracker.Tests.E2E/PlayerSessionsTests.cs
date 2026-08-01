using System.Text.RegularExpressions;
using Microsoft.Playwright;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

[Collection("E2E")]
public sealed class PlayerSessionsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;
    private Guid _publicUser;
    private Guid _privateUser;

    public PlayerSessionsTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();

        _publicUser = await _fixture.Seed.SeedUserAsync("SessionHero", isPublic: true);
        _privateUser = await _fixture.Seed.SeedUserAsync("SecretPlayer", isPublic: false);
        var passChart = await _fixture.Seed.SeedPhoenixChartAsync("Session Anthem", 21, "Double");
        var upscoreChart = await _fixture.Seed.SeedPhoenixChartAsync("Journal Groove", 19, "Single");

        // One import session: an earlier pass on the upscore chart, then the session's
        // two rows (a new pass and the upscore), a Pumbility milestone, and a crown
        // highlight — the full roundup anatomy.
        var sessionId = Guid.NewGuid();
        await _fixture.Seed.SeedJournalRowAsync(_publicUser, upscoreChart, Now.AddDays(-10), 900000,
            "FairGame", isBroken: false, sessionId: null);
        await _fixture.Seed.SeedJournalRowAsync(_publicUser, passChart, Now.AddMinutes(-3), 951234,
            "SuperbGame", isBroken: false, sessionId: sessionId, source: "officialImport");
        await _fixture.Seed.SeedJournalRowAsync(_publicUser, upscoreChart, Now, 962500,
            "SuperbGame", isBroken: false, sessionId: sessionId, source: "officialImport");
        await _fixture.Seed.SeedMilestoneAsync(_publicUser, sessionId, Now, "PumbilityGain",
            oldValue: 8000, newValue: 8100);
        await _fixture.Seed.SeedHighlightAsync(_publicUser, passChart, sessionId, Now.AddMinutes(-3),
            flags: 1 /* PumbilityTop50 */, level: 21, pumbilityRank: 4);

        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task SessionsPageRendersTheNewestSessionAsAHeroWithTheRestAsRows()
    {
        await _page.GotoAsync($"/Player/{_publicUser}/Sessions");

        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        await Expect(_page.GetByText("SessionHero — Sessions")).ToBeVisibleAsync(timeout);

        // ONE hero, not a card per session — that is the whole shape of the overhaul.
        var hero = _page.Locator("[data-testid='session-hero']");
        await Expect(hero).ToHaveCountAsync(1, new LocatorAssertionsToHaveCountOptions { Timeout = 60_000 });

        // The answer above the fold: the ceremony band carries the Pumbility movement.
        await Expect(_page.Locator("[data-testid='session-ceremony']")).ToBeVisibleAsync(timeout);
        await Expect(_page.GetByText("8,100").First).ToBeVisibleAsync();

        // Everything older is a row with a View button rather than a second card.
        await Expect(_page.Locator("[data-testid='session-history']")).ToBeVisibleAsync(timeout);

        // All plays holds the session's journal, breaks included, as an ordinary log.
        var plays = _page.Locator("[data-testid='session-all-plays']");
        await Expect(plays).ToBeVisibleAsync(timeout);
        await Expect(plays.GetByText("Session Anthem")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NonPublicPlayersRedirectHome()
    {
        await _page.GotoAsync($"/Player/{_privateUser}/Sessions");

        // The page bounces to home, which itself may forward anonymous visitors —
        // the invariant is that a private player's sessions never render.
        await _page.WaitForURLAsync(url => !url.Contains("/Sessions"),
            new PageWaitForURLOptions { Timeout = 60_000 });
        Assert.DoesNotContain("/Player/", _page.Url);
        await Expect(_page.GetByText("SecretPlayer")).ToHaveCountAsync(0);
    }

}
