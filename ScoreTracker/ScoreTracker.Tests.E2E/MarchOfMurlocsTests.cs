using System.Text.RegularExpressions;
using Microsoft.Playwright;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.E2E.Support;
using static Microsoft.Playwright.Assertions;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     March of Murlocs Slice 4a (docs/design/march-of-murlocs.md §12.2), the one whole-workflow
///     fact: the season page is real HTML before any circuit — the board, the title, the unfurl card —
///     and a board row opens the session breakdown, whose circuit then connects and draws the four
///     numbers. The retired routes 301 into the section.
/// </summary>
[Collection("E2E")]
public sealed class MarchOfMurlocsTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private IBrowserContext _browser = null!;
    private IPage _page = null!;
    private Guid _boardId;
    private Guid _phoenix2Board;
    private Guid _kimSession;

    public MarchOfMurlocsTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        var slam = await _fixture.Seed.SeedPhoenixChartAsync("Slam", 24, "Double");
        var kim = await _fixture.Seed.SeedUserAsync("KIMJAEHYUN");
        var rival = await _fixture.Seed.SeedUserAsync("YIMMYTHE42");
        var now = DateTimeOffset.UtcNow;
        Guid seasonId;
        (seasonId, _boardId) = await _fixture.Seed.SeedMoMSeasonAsync("Winter 2099", now.AddDays(-20), now.AddDays(60));
        // The Phoenix 2 Doubles board of the same season, empty (D38): a Phoenix 2 visitor sees it.
        _phoenix2Board = await _fixture.Seed.SeedMoMBoardAsync(seasonId, "Winter 2099", MixIds.Phoenix2);
        _kimSession = await _fixture.Seed.SeedMoMSessionAsync(_boardId, kim, slam, 976489, 59319, now.AddDays(-7));
        await _fixture.Seed.SeedMoMSessionAsync(_boardId, rival, slam, 983047, 57325, now.AddDays(-5));
        _browser = await _fixture.NewBrowserContextAsync();
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    /// <summary>
    ///     The static payoff: an anonymous request carries the season, the ranked board and the head
    ///     before any JS — what a crawler and a link unfurler receive. The old routes land here too.
    /// </summary>
    [Fact]
    public async Task TheSeasonAndItsBoardAreInTheHtmlBeforeAnyCircuitAndTheOldRoutesRedirectHere()
    {
        var response = await _page.APIRequest.GetAsync($"{_fixture.BaseUrl}/MarchOfMurlocs");
        var html = await response.TextAsync();

        Assert.Contains("Winter 2099", html);
        Assert.Contains("59,319", html);
        Assert.Contains("57,325", html);
        Assert.Contains("KIMJAEHYUN", html);
        Assert.Contains($"/MarchOfMurlocs/Session/{_kimSession}", html);
        Assert.Contains("<title>March of Murlocs", html);
        Assert.Contains("og:description", html);
        Assert.Contains("data-mom-seasons", html); // the Past-seasons chip, inert until the island connects
        // D44: a logged-out visitor has never published, so the newcomer card is in the HTML with
        // its Read-the-rules button, and the frame carries the How-it-works chip.
        Assert.Contains("data-testid=\"mom-howto\"", html);
        Assert.Contains("data-testid=\"mom-howto-rules\"", html);
        Assert.Contains("href=\"/MarchOfMurlocs/Rules\"", html);

        var directory = await _page.APIRequest.GetAsync($"{_fixture.BaseUrl}/Tournaments/MarchOfMurlocs",
            new APIRequestContextOptions { MaxRedirects = 0 });
        Assert.Equal(301, directory.Status);
        Assert.Equal("/MarchOfMurlocs", directory.Headers["location"]);

        var board = await _page.APIRequest.GetAsync($"{_fixture.BaseUrl}/Tournament/Stamina/{_boardId}",
            new APIRequestContextOptions { MaxRedirects = 0 });
        Assert.Equal(301, board.Status);
        Assert.Equal("/MarchOfMurlocs?board=Double", board.Headers["location"]);
    }

    /// <summary>
    ///     The rules of record are real HTML at their own URL, with their head and a sitemap entry,
    ///     and the season page's two rules links land there (D42).
    /// </summary>
    [Fact]
    public async Task TheRulesPageIsHtmlBeforeAnyCircuitAndTheSeasonLinksToIt()
    {
        var rules = await _page.APIRequest.GetAsync($"{_fixture.BaseUrl}/MarchOfMurlocs/Rules");
        var html = await rules.TextAsync();

        Assert.Equal(200, rules.Status);
        Assert.Contains("<title>March of Murlocs", html);
        Assert.Contains("How March of Murlocs works", html);
        Assert.Contains("data-testid=\"mom-rl-grades\"", html);
        Assert.Contains("data-testid=\"mom-rl-example\"", html);
        Assert.Contains("og:description", html);
        Assert.Contains("/MarchOfMurlocs/Rules", html); // the canonical link

        var season = await _page.APIRequest.GetAsync($"{_fixture.BaseUrl}/MarchOfMurlocs");
        var seasonHtml = await season.TextAsync();
        Assert.Contains("href=\"/MarchOfMurlocs/Rules\"", seasonHtml);
        Assert.DoesNotContain("docs.google.com", seasonHtml);

        var sitemap = await _page.APIRequest.GetAsync($"{_fixture.BaseUrl}/sitemap.xml");
        Assert.Contains("https://piuscores.arroweclip.se/MarchOfMurlocs/Rules", await sitemap.TextAsync());
    }

    /// <summary>A Phoenix 2 visitor sees the season's Phoenix 2 board, live and empty, not a notice (D38).</summary>
    [Fact]
    public async Task APhoenixTwoVisitorSeesThePhoenixTwoBoard()
    {
        await _browser.AddCookiesAsync(new[]
        {
            new Cookie { Name = "CurrentMix", Value = "Phoenix2", Url = _fixture.BaseUrl }
        });

        await _page.GotoAsync($"{_fixture.BaseUrl}/MarchOfMurlocs");

        await Expect(_page.Locator(".pmb-eyebrow")).ToContainTextAsync("Phoenix 2 · March of Murlocs");
        await Expect(_page.Locator("[data-testid=mom-board]")).ToBeVisibleAsync();
        await Expect(_page.Locator("[data-testid=mom-no-boards]")).ToHaveCountAsync(0);
        await Expect(_page.Locator("[data-testid=mom-board-row]")).ToHaveCountAsync(0);
        Assert.DoesNotContain("scoring is settled", await _page.ContentAsync());
    }

    /// <summary>A board row is the way into a session; the breakdown's circuit draws the four numbers.</summary>
    [Fact]
    public async Task ABoardRowOpensTheSessionBreakdown()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/MarchOfMurlocs");
        var rows = _page.Locator("[data-testid=mom-board-row]");
        await Expect(rows).ToHaveCountAsync(2);
        await Expect(rows.First).ToContainTextAsync("59,319");

        await rows.First.ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex($"/MarchOfMurlocs/Session/{_kimSession}$", RegexOptions.IgnoreCase));
        await Expect(_page.Locator("[data-testid=mom-hero-total]")).ToHaveTextAsync("59,319");
        await Expect(_page.Locator("[data-testid=mom-hero-place]")).ToHaveTextAsync("1st");
        await Expect(_page.Locator("[data-testid=mom-four]")).ToBeVisibleAsync();
        await Expect(_page.Locator("[data-testid=mom-chart-card]")).ToHaveCountAsync(1);
        await Expect(_page.Locator("[data-testid=mom-compare]")).ToBeVisibleAsync();
    }

    /// <summary>
    ///     The site's critical March of Murlocs journey (§12.3): open a draft from the season page,
    ///     fill it from the score journal, publish it, and find it on the board. Every step is a
    ///     real circuit against a real database — the import re-reads the journal, so what lands is
    ///     what was played rather than anything the page asserted.
    /// </summary>
    [Fact]
    public async Task ANightIsDraftedImportedPublishedAndLandsOnTheBoard()
    {
        var me = await _fixture.Seed.SeedUserAsync("DRMURLOC");
        var slam = await _fixture.Seed.SeedPhoenixChartAsync("Slam", 24, "Double");
        var gargoyle = await _fixture.Seed.SeedPhoenixChartAsync("Gargoyle", 20, "Double");
        var now = DateTimeOffset.UtcNow;
        var (seasonId, _) = await _fixture.Seed.SeedMoMSeasonAsync("Recordable 2099", now.AddDays(-10),
            now.AddDays(50));
        // A board with real level ratings: a chart that prices to zero cannot enter a session.
        var board = await _fixture.Seed.SeedMoMBoardAsync(seasonId, "Recordable 2099", MixIds.Phoenix,
            levelRatings: new Dictionary<int, int> { [20] = 650, [24] = 1450 });

        // A night in the journal, twenty minutes apart, plus a stray play a day earlier that the
        // fifteen-minute split has to leave in its own block.
        var night = now.AddHours(-3);
        await _fixture.Seed.SeedJournalRowAsync(me, slam, night, 980000, "MarvelousGame", false, null,
            "officialImport");
        await _fixture.Seed.SeedJournalRowAsync(me, gargoyle, night.AddMinutes(4), 986121, "MarvelousGame",
            false, null, "officialImport");
        await _fixture.Seed.SeedJournalRowAsync(me, slam, now.AddDays(-1), 900000, "SuperbGame", false, null,
            "officialImport");

        await _page.GotoAsync($"{_fixture.BaseUrl}/Login");
        await _page.EvaluateAsync(
            "id => fetch('/Login/Dev', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'userId=' + id })",
            me.ToString());

        // The season page's Record chip opens a draft and hands over to the session's own URL.
        await _page.GotoAsync($"{_fixture.BaseUrl}/MarchOfMurlocs/Record/{board}");
        await Expect(_page).ToHaveURLAsync(new Regex("/MarchOfMurlocs/Session/.+/Edit$", RegexOptions.IgnoreCase),
            new PageAssertionsToHaveURLOptions { Timeout = 60_000 });
        await Expect(_page.Locator("[data-testid=mom-submit-state]")).ToContainTextAsync("Draft");
        await Expect(_page.Locator("[data-testid=mom-session-empty]")).ToBeVisibleAsync();

        await _page.Locator("[data-testid=mom-open-import]").ClickAsync();

        // The dialog opens on the block worth most, which is tonight's two plays, not yesterday's.
        var add = _page.Locator("[data-testid=mom-import-add]");
        await Expect(add).ToContainTextAsync("Add 2 charts", new LocatorAssertionsToContainTextOptions { Timeout = 60_000 });
        await Expect(_page.Locator("[data-testid=mom-import-play]")).ToHaveCountAsync(3);
        await add.ClickAsync();

        await Expect(_page.Locator("[data-testid=mom-session-row]")).ToHaveCountAsync(2);
        await Expect(_page.Locator("[data-testid=mom-budget]")).ToContainTextAsync("2 charts");

        await _page.Locator("[data-testid=mom-publish]").ClickAsync();
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Publish", Exact = true })
            .Last.ClickAsync();

        await Expect(_page.Locator("[data-testid=mom-published]")).ToBeVisibleAsync(
            new LocatorAssertionsToBeVisibleOptions { Timeout = 60_000 });
        await Expect(_page.Locator("[data-testid=mom-submit-state]")).ToContainTextAsync("on the board");

        // And it is on the board, which is the whole point.
        var seasonHtml = await (await _page.APIRequest.GetAsync($"{_fixture.BaseUrl}/MarchOfMurlocs")).TextAsync();
        Assert.Contains("DRMURLOC", seasonHtml);
        Assert.Contains("Recordable 2099", seasonHtml);
    }
}
