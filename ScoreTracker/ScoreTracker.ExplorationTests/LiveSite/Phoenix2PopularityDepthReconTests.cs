using HtmlAgilityPack;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Live recon for the play-ranking endpoint (/ajax/top_steps.php — the same call the
///     site's "+ More" button makes). Answers three structural questions the mirror
///     depends on: how deep the ranking really serves (raw li counts per offset, walked
///     to exhaustion), how often a full page carries tiles the strict parser would skip
///     (the sweep's termination condition conflates the two), and whether the endpoint
///     has a songs mode. Dumps land in %TEMP%\p2-popularity-recon for markup inspection.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Phoenix2PopularityDepthReconTests : IClassFixture<PiuGameSessionFixture>
{
    private const int OffsetCap = 20_000;
    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Phoenix2PopularityDepthReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [LiveSiteFact]
    public async Task PopularityEndpointDepthAndModesAreKnown()
    {
        var ct = CancellationToken.None;
        var (client, sid) = await _fixture.Api.GetSessionId(MixEnum.Phoenix2,
            PiuGameSessionFixture.Username!, PiuGameSessionFixture.Password!, ct);
        Assert.False(string.IsNullOrWhiteSpace(sid), "Phoenix 2 login produced no session id.");

        var dumpDir = Path.Combine(Path.GetTempPath(), "p2-popularity-recon");
        Directory.CreateDirectory(dumpDir);

        // 1 — the human page, for the inline JS the More button runs (params, modes).
        foreach (var pageUrl in new[]
                 {
                     "https://piugame.com/leaderboard/top_steps.php",
                     "https://piugame.com/top_steps.php"
                 })
        {
            var response = await client.GetAsync(pageUrl, ct);
            var html = await response.Content.ReadAsStringAsync(ct);
            var fileName = $"page_{Math.Abs(pageUrl.GetHashCode())}.html";
            if (response.IsSuccessStatusCode)
                await File.WriteAllTextAsync(Path.Combine(dumpDir, fileName), html, ct);
            _output.WriteLine(
                $"human page {pageUrl}: HTTP {(int)response.StatusCode}, {html.Length} chars, " +
                $"mentionsAjax={html.Contains("top_steps", StringComparison.OrdinalIgnoreCase)} → {fileName}");
        }

        // 2 — raw walk of the ajax endpoint to exhaustion.
        var date = $"{DateTimeOffset.UtcNow.AddDays(-1).Year}{DateTimeOffset.UtcNow.AddDays(-1).Month:00}";
        var offset = 0;
        var totalRaw = 0;
        var totalParseable = 0;
        var pages = 0;
        var shortPages = 0;
        var deepestPlace = 0;
        while (offset <= OffsetCap)
        {
            var html = await PostAjax(client, offset, date, "full", ct);
            var (raw, parseable, lastPlace) = Inspect(html);
            totalRaw += raw;
            totalParseable += parseable;
            pages++;
            if (lastPlace > deepestPlace) deepestPlace = lastPlace;
            if (raw == 50 && parseable < 50) shortPages++;
            if (offset == 0)
                await File.WriteAllTextAsync(Path.Combine(dumpDir, "ajax_offset_0.html"), html, ct);
            if (offset == 1000)
                await File.WriteAllTextAsync(Path.Combine(dumpDir, "ajax_offset_1000.html"), html, ct);
            if (pages % 20 == 0 || raw < 50)
                _output.WriteLine(
                    $"offset {offset}: raw={raw} parseable={parseable} lastPlace={lastPlace}");
            if (raw < 50)
            {
                await File.WriteAllTextAsync(Path.Combine(dumpDir, "ajax_final_page.html"), html, ct);
                break;
            }

            offset += 50;
            await Task.Delay(250, ct);
        }

        _output.WriteLine(
            $"walk: {pages} pages, {totalRaw} raw rows, {totalParseable} parseable, " +
            $"deepest place {deepestPlace}, full-but-unparseable pages: {shortPages}");
        Assert.True(totalRaw > 0, "The play ranking served zero rows — auth, date, or markup drift.");

        // 3 — songs-mode probe: the site's song tab looks like the same mechanism.
        foreach (var mode in new[] { "song", "songs", "music", "half", "" })
        {
            var html = await PostAjax(client, 0, date, mode, ct);
            var (raw, parseable, _) = Inspect(html);
            var hasStepBall = html.Contains("stepBall", StringComparison.OrdinalIgnoreCase);
            _output.WriteLine(
                $"mode '{mode}': {html.Length} chars, raw={raw} parseable={parseable} stepBall={hasStepBall}");
            if (raw > 0 && !hasStepBall)
                await File.WriteAllTextAsync(Path.Combine(dumpDir, $"ajax_mode_{mode}.html"), html, ct);
            await Task.Delay(250, ct);
        }
    }

    /// <summary>
    ///     The song ranking is a sibling page (/leaderboard/top_songs.php); this pins whether
    ///     its ajax twin exists, how its tiles look (no stepball expected), and how deep it
    ///     serves — the groundwork for mirroring the official song board.
    /// </summary>
    [LiveSiteFact]
    public async Task SongRankingEndpointShapeIsKnown()
    {
        var ct = CancellationToken.None;
        var (client, sid) = await _fixture.Api.GetSessionId(MixEnum.Phoenix2,
            PiuGameSessionFixture.Username!, PiuGameSessionFixture.Password!, ct);
        Assert.False(string.IsNullOrWhiteSpace(sid), "Phoenix 2 login produced no session id.");

        var dumpDir = Path.Combine(Path.GetTempPath(), "p2-popularity-recon");
        Directory.CreateDirectory(dumpDir);
        var date = $"{DateTimeOffset.UtcNow.AddDays(-1).Year}{DateTimeOffset.UtcNow.AddDays(-1).Month:00}";

        var page = await client.GetAsync("https://piugame.com/leaderboard/top_songs.php", ct);
        var pageHtml = await page.Content.ReadAsStringAsync(ct);
        if (page.IsSuccessStatusCode)
            await File.WriteAllTextAsync(Path.Combine(dumpDir, "top_songs_page.html"), pageHtml, ct);
        _output.WriteLine($"song page: HTTP {(int)page.StatusCode}, {pageHtml.Length} chars");

        foreach (var offset in new[] { 0, 500, 5000 })
        {
            var response = await client.PostAsync("https://piugame.com/ajax/top_songs.php",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "page", offset.ToString() },
                    { "date", date },
                    { "mode", "full" }
                }), ct);
            var html = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : "";
            var document = new HtmlDocument();
            document.LoadHtml(html);
            var lis = document.DocumentNode.SelectNodes("./li")?.Count ?? 0;
            var names = document.DocumentNode.SelectNodes(
                ".//div[contains(@class,'profile_name')]/p[contains(@class,'t1')]")?.Count ?? 0;
            _output.WriteLine(
                $"song ajax offset {offset}: HTTP {(int)response.StatusCode}, {html.Length} chars, " +
                $"raw={lis} names={names} stepBall={html.Contains("stepBall")}");
            if (offset == 0 && lis > 0)
                await File.WriteAllTextAsync(Path.Combine(dumpDir, "top_songs_ajax_0.html"), html, ct);
            await Task.Delay(250, ct);
        }
    }

    private static async Task<string> PostAjax(HttpClient client, int offset, string date, string mode,
        CancellationToken ct)
    {
        var response = await client.PostAsync("https://piugame.com/ajax/top_steps.php",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "page", offset.ToString() },
                { "date", date },
                { "mode", mode }
            }), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>
    ///     Raw tile count vs how many the sweep's strict parser would keep, plus the
    ///     deepest place number printed on the page.
    /// </summary>
    private static (int Raw, int Parseable, int LastPlace) Inspect(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var lis = document.DocumentNode.SelectNodes("./li");
        if (lis == null) return (0, 0, 0);

        var parseable = 0;
        var lastPlace = 0;
        foreach (var li in lis)
        {
            var place = li.SelectSingleNode(".//div[contains(@class,'num')]/i[contains(@class,'tt')]");
            if (place != null && int.TryParse(place.InnerText, out var parsed) && parsed > lastPlace)
                lastPlace = parsed;
            var hasMedal = li.SelectSingleNode(".//span[contains(@class,'medal_wrap')]//img") != null;
            var hasName = li.SelectSingleNode(
                ".//div[contains(@class,'profile_name')]/p[contains(@class,'t1')]") != null;
            var hasBall = li.SelectSingleNode(
                ".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'numw')]//img") != null;
            if ((place != null || hasMedal) && hasName && hasBall) parseable++;
        }

        return (lis.Count, parseable, lastPlace);
    }
}
