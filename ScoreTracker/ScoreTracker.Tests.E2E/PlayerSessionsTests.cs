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
            flags: 1 /* PumbilityTop50 */, level: 21);

        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task SessionsPageRendersRoundupCardsWithTheFullBreakdownBehindADialog()
    {
        await _page.GotoAsync($"/Player/{_publicUser}/Sessions");

        var timeout = new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 };
        await Expect(_page.GetByText("SessionHero — Sessions")).ToBeVisibleAsync(timeout);

        // Two groups: the import session and the pre-capture day group.
        var cards = _page.Locator("[data-testid='session-card']");
        await Expect(cards).ToHaveCountAsync(2, new LocatorAssertionsToHaveCountOptions { Timeout = 60_000 });

        // The milestone strip renders the Pumbility gain.
        await Expect(_page.Locator("[data-testid='milestone-strip']")).ToBeVisibleAsync(timeout);
        await Expect(_page.GetByText("8,000 → 8,100")).ToBeVisibleAsync();

        // The card leads with the flagged row; the full breakdown opens as a dialog.
        await Expect(_page.GetByText("New Pass").First).ToBeVisibleAsync();
        await cards.First.Locator("[data-testid='view-all-scores']").ClickAsync();
        var dialog = _page.Locator("[data-testid='session-scores-dialog']");
        await Expect(dialog).ToBeVisibleAsync(timeout);
        await Expect(dialog.GetByText("Session Anthem")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Journal Groove")).ToBeVisibleAsync();
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
