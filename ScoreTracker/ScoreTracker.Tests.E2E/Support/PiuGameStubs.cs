using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ScoreTracker.Tests.E2E.Support;

/// <summary>
///     Maps a WireMock server to answer as phoenix.piugame.com using the snapshot fixtures in
///     PiuGame/Fixtures — PII-scrubbed raw captures of the real site (2026-07-03). The app
///     under test points PiuGame:BaseUrl (and AmPassUrl) here, so a PIUGAME login or score
///     import never leaves the machine.
/// </summary>
internal static class PiuGameStubs
{
    /// <summary>The scrubbed account name served by AccountTitles.html / GameCards.html.</summary>
    public const string GameTag = "E2EPLAYER";

    /// <summary>
    ///     GameCards.html carries TWO cards, like the captured account: the active one
    ///     (E2EPLAYER, this id) and an alt (E2EALT, 9990002). The import page therefore asks
    ///     for a card confirmation on the first Import click — tests follow that real flow.
    /// </summary>
    public const string CardId = "9990001";

    public const string SessionId = "e2e-session-token";

    public static void MapPiuGameSite(this WireMockServer server)
    {
        // GetSessionId only reads the sid* cookie the login POST deposits.
        server.Given(Request.Create().WithPath("/bbs/login_check.php").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Set-Cookie", $"sid={SessionId}; Path=/")
                .WithBody("OK"));

        server.Given(Request.Create().WithPath("/my_page/title.php").UsingGet())
            .RespondWith(HtmlFixture("AccountTitles.html"));

        server.Given(Request.Create().WithPath("/my_page/game_id_information.php").UsingGet())
            .RespondWith(HtmlFixture("GameCards.html"));

        // Served for every page number: the fixture has no last-page button, so the parser
        // reports MaxPage == the requested page and the import stays a single-page read.
        server.Given(Request.Create().WithPath("/my_page/my_best_score.php").UsingGet())
            .RespondWith(HtmlFixture("BestScores_SinglePage.html"));

        server.Given(Request.Create().WithPath("/my_page/recently_played.php").UsingGet())
            .RespondWith(HtmlFixture("RecentlyPlayed.html"));

        server.Given(Request.Create().WithPath("/ajax/sub_profile.php").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("OK"));

        // The login flow's warm-up GET of the site root and the stubbed am-pass hop just
        // need any 200 that is not an /ssoc bounce.
        server.Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("<html></html>"));
    }

    /// <summary>
    ///     The Phoenix 2 best list as the console script walks it: two trimmed pages of the redesigned
    ///     markup whose pager ends the list at page 2, and a bare root page to run the script on, since
    ///     it fetches same-origin the way it does from the player's own tab. Maps nothing else, so a
    ///     test calls it on a reset server after any login it needs.
    /// </summary>
    public static void MapPhoenix2BestScorePages(this WireMockServer server)
    {
        server.Given(Request.Create().WithPath("/my_page/my_best_score.php").WithParam("page", "1").UsingGet())
            .RespondWith(HtmlFixture("BestScores_Phoenix2_Page1.html"));
        server.Given(Request.Create().WithPath("/my_page/my_best_score.php").WithParam("page", "2").UsingGet())
            .RespondWith(HtmlFixture("BestScores_Phoenix2_Page2.html"));
        server.Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<!DOCTYPE html><html><body></body></html>"));
    }

    /// <summary>
    ///     The wrong-password shape: piugame still issues a session cookie, but the
    ///     account page carries no profile/title list, which GetAccountData reports as
    ///     AccountName "INVALID" and the login flow maps to InvalidCredentialException.
    /// </summary>
    public static void MapPiuGameInvalidLogin(this WireMockServer server)
    {
        server.Given(Request.Create().WithPath("/bbs/login_check.php").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Set-Cookie", $"sid={SessionId}; Path=/")
                .WithBody("OK"));

        server.Given(Request.Create().WithPath("/my_page/title.php").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><body><div class=\"login_wrap\"></div></body></html>"));

        server.Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("<html></html>"));
    }

    private static IResponseBuilder HtmlFixture(string fileName)
    {
        return Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "text/html; charset=utf-8")
            .WithBodyFromFile(Path.Combine(AppContext.BaseDirectory, "PiuGame", "Fixtures", fileName));
    }
}
