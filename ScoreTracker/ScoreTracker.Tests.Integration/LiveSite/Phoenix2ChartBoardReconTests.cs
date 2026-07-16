using HtmlAgilityPack;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.Tests.Integration.LiveSite;

/// <summary>
///     Live canary for the Phoenix 2 chart boards (over_ranking_view). Pins the structural
///     facts the sweep parser depends on: the login-gated 20+ song list enumerates, an
///     individual board parses rows, and the board's depth/pagination shape is what the
///     scraper implements. The output log carries the raw observations (row count, paging
///     icons, page-2 behavior) for diagnosing site drift.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Phoenix2ChartBoardReconTests : IClassFixture<PiuGameSessionFixture>
{
    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Phoenix2ChartBoardReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [LiveSiteFact]
    public async Task ChartBoardServesRowsAndItsPaginationShapeIsKnown()
    {
        var ct = CancellationToken.None;
        var (client, sid) = await _fixture.Api.GetSessionId(MixEnum.Phoenix2,
            PiuGameSessionFixture.Username!, PiuGameSessionFixture.Password!, ct);
        Assert.False(string.IsNullOrWhiteSpace(sid), "Phoenix 2 login produced no session id.");

        var dumpDir = Path.Combine(Path.GetTempPath(), "p2-board-recon");
        Directory.CreateDirectory(dumpDir);

        // Auth sanity: the PUMBILITY board is known to serve this session — zero rows here
        // means the session died, not that the chart list moved.
        var pumbility = await _fixture.Api.GetPumbilityRankings(MixEnum.Phoenix2, null, 1, client, ct);
        _output.WriteLine($"pumbility sanity: {pumbility.Entries.Length} rows, IsEnd={pumbility.IsEnd}");

        // Raw-fetch the 20+ list so the recon still reports when the strict parser's
        // markup assumptions have drifted; board ids come from the link pattern alone.
        string[] boardIds = Array.Empty<string>();
        foreach (var listUrl in new[]
                 {
                     "https://piugame.com/leaderboard/over_ranking.php?lv=&search=&&page=1",
                     "https://piugame.com/leaderboard/over_ranking.php",
                     "https://piugame.com/leaderboard/over_ranking.php?lv=24"
                 })
        {
            var listResponse = await client.GetAsync(listUrl, ct);
            var listHtml = await listResponse.Content.ReadAsStringAsync(ct);
            var fileName = $"list_{Math.Abs(listUrl.GetHashCode())}.html";
            await File.WriteAllTextAsync(Path.Combine(dumpDir, fileName), listHtml, ct);
            var listDocument = new HtmlDocument();
            listDocument.LoadHtml(listHtml);
            var listRows = listDocument.DocumentNode
                .SelectNodes("//ul[contains(@class,'rating_ranking_list')]//div[contains(@class,'li_in')]")
                ?.Count ?? 0;
            var ids = System.Text.RegularExpressions.Regex
                .Matches(listHtml, @"over_ranking_view\.php\?no=([a-zA-Z0-9+/=%]+)")
                .Select(m => m.Groups[1].Value).Distinct().Take(3).ToArray();
            _output.WriteLine(
                $"list {listUrl}: HTTP {(int)listResponse.StatusCode}, {listHtml.Length} chars, " +
                $"strict-parse rows={listRows}, regex ids={ids.Length}, " +
                $"errorPage={listHtml.Contains("오류안내")} → {fileName}");
            if (ids.Length > 0 && boardIds.Length == 0) boardIds = ids;
        }

        Assert.NotEmpty(boardIds);

        foreach (var boardId in boardIds)
        {
            var page1 = await FetchBoardHtml(client, boardId, page: null, ct);
            var page2 = await FetchBoardHtml(client, boardId, page: 2, ct);
            var rows1 = CountRows(page1);
            var rows2 = CountRows(page2);
            var firstRow1 = FirstProfileName(page1);
            var firstRow2 = FirstProfileName(page2);
            _output.WriteLine(
                $"board {boardId}: page1 rows={rows1} " +
                $"nextIcon={HasIcon(page1, "next")} lastIcon={HasIcon(page1, "last")} first='{firstRow1}' | " +
                $"page2 rows={rows2} first='{firstRow2}' samePage={firstRow1 == firstRow2}");
            await File.WriteAllTextAsync(Path.Combine(dumpDir, $"board_{boardId.Replace("/", "_").Replace("=", "")}_p1.html"), page1, ct);
            await File.WriteAllTextAsync(Path.Combine(dumpDir, $"board_{boardId.Replace("/", "_").Replace("=", "")}_p2.html"), page2, ct);

            Assert.True(rows1 > 0, $"Board {boardId} parsed zero rows — over_ranking_view markup drifted.");
        }
    }

    private async Task<string> FetchBoardHtml(HttpClient client, string songId, int? page, CancellationToken ct)
    {
        var url = $"https://piugame.com/leaderboard/over_ranking_view.php?no={songId}" +
                  (page == null ? string.Empty : $"&page={page}");
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static int CountRows(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document.DocumentNode.SelectNodes("//div[contains(@class,'rangking_list_w')]//li")?.Count ?? 0;
    }

    private static string FirstProfileName(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var node = document.DocumentNode.SelectSingleNode(
            "//div[contains(@class,'rangking_list_w')]//li//div[contains(@class,'profile_name')]");
        return node?.InnerText.Trim() ?? string.Empty;
    }

    private static bool HasIcon(string html, string kind)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var nodes = document.DocumentNode.SelectNodes($"//i[contains(@class,'{kind}')]");
        return nodes != null && nodes.Count > 0;
    }
}
