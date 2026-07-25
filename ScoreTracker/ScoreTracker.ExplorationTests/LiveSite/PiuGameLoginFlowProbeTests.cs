using System.Net;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Diagnostic probe for the piugame login flow: replays the login_check POST step by
///     step and reports the response shape (status, redirect target, body markers, which
///     cookies exist — never their values). Exists to answer "why did every authenticated
///     live test start failing" without spelunking; it asserts nothing beyond HTTP success.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PiuGameLoginFlowProbeTests
{
    private readonly ITestOutputHelper _output;

    public PiuGameLoginFlowProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [LiveSiteFact]
    public async Task LoginCheckResponseShapeIsObservable()
    {
        var ct = CancellationToken.None;
        var dumpDir = Path.Combine(Path.GetTempPath(), "p2-board-recon");
        Directory.CreateDirectory(dumpDir);
        var cookies = new CookieContainer();
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies });
        client.DefaultRequestHeaders.Add("origin", "https://phoenix.piugame.com");

        var landing = await client.GetAsync("https://phoenix.piugame.com", ct);
        _output.WriteLine($"landing: HTTP {(int)landing.StatusCode} via {landing.RequestMessage?.RequestUri}");

        var response = await client.PostAsync("https://phoenix.piugame.com/bbs/login_check.php",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "url", "/" },
                { "mb_id", PiuGameSessionFixture.Username! },
                { "mb_password", PiuGameSessionFixture.Password! }
            }), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        await File.WriteAllTextAsync(Path.Combine(dumpDir, "login_check_response.html"), body, ct);

        var cookieNames = string.Join(",", cookies.GetCookies(new Uri("https://phoenix.piugame.com"))
            .Select(c => c.Name));
        _output.WriteLine($"login_check: HTTP {(int)response.StatusCode} " +
                          $"landed={response.RequestMessage?.RequestUri} bodyChars={body.Length} " +
                          $"cookies=[{cookieNames}]");
        _output.WriteLine($"markers: alert={body.Contains("alert(")} amPass={body.Contains("am-pass")} " +
                          $"captcha={body.Contains("captcha", StringComparison.OrdinalIgnoreCase)} " +
                          $"errorPage={body.Contains("오류안내")}");

        var myPage = await client.GetAsync("https://phoenix.piugame.com/my_page/play_data.php", ct);
        var myPageBody = await myPage.Content.ReadAsStringAsync(ct);
        await File.WriteAllTextAsync(Path.Combine(dumpDir, "my_page_after_login.html"), myPageBody, ct);
        _output.WriteLine($"my_page: HTTP {(int)myPage.StatusCode} chars={myPageBody.Length} " +
                          $"loginForm={myPageBody.Contains("login_fs")}");
    }
}
