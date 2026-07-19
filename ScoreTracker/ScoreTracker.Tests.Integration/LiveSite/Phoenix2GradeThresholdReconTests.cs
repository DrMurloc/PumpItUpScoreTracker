using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.Tests.Integration.LiveSite;

/// <summary>
///     Empirical recon for the Phoenix 2 letter-grade thresholds. The official pages render a
///     grade image (<c>/l_img/grade/{stem}.png</c>) next to each raw score, so pairing the two
///     across a busy board (which spans ~700k–1,000,000 in a single page) pins every grade
///     boundary the site is actually using. For each observed (score, site-grade) pair this
///     also computes our own code's grade via <see cref="PhoenixScore.LetterGrade" /> and reports
///     the min/max site score per grade plus every disagreement — that mismatch list is the
///     answer to "did the thresholds move, and to what". Read-only, login-gated, manual-run.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Phoenix2GradeThresholdReconTests : IClassFixture<PiuGameSessionFixture>
{
    private static readonly string DumpDir =
        Environment.GetEnvironmentVariable("PIU_RECON_DUMP_DIR")
        ?? Path.Combine(Path.GetTempPath(), "p2-grade-recon");

    // Grade-image filename stem (lowercased, "_p" = plus) -> our enum value.
    private static readonly IReadOnlyDictionary<string, PhoenixLetterGrade> GradeByStem =
        new Dictionary<string, PhoenixLetterGrade>
        {
            ["f"] = PhoenixLetterGrade.F,
            ["d"] = PhoenixLetterGrade.D,
            ["c"] = PhoenixLetterGrade.C,
            ["b"] = PhoenixLetterGrade.B,
            ["a"] = PhoenixLetterGrade.A,
            ["a_p"] = PhoenixLetterGrade.APlus,
            ["aa"] = PhoenixLetterGrade.AA,
            ["aa_p"] = PhoenixLetterGrade.AAPlus,
            ["aaa"] = PhoenixLetterGrade.AAA,
            ["aaa_p"] = PhoenixLetterGrade.AAAPlus,
            ["s"] = PhoenixLetterGrade.S,
            ["s_p"] = PhoenixLetterGrade.SPlus,
            ["ss"] = PhoenixLetterGrade.SS,
            ["ss_p"] = PhoenixLetterGrade.SSPlus,
            ["sss"] = PhoenixLetterGrade.SSS,
            ["sss_p"] = PhoenixLetterGrade.SSSPlus
        };

    // Phoenix 1 serves /l_img/grade/, Phoenix 2 serves /l_img/p2/grade/ — match both.
    private static readonly Regex GradeStemRegex =
        new(@"/grade/([a-z_]+)\.png", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ScoreDigitsRegex = new(@"[\d,]{4,}", RegexOptions.Compiled);

    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Phoenix2GradeThresholdReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private readonly record struct Observation(int Score, string Stem, PhoenixLetterGrade SiteGrade, string Source);

    /// <summary>
    ///     The decisive comparison: Phoenix 1 (phoenix.piugame.com) serves boards anonymously,
    ///     so we can read its grade-vs-score pairs with no login. If P1 grades still agree with
    ///     our current thresholds, the new scheme is Phoenix-2-only (per-mix grade resolution
    ///     needed); if P1 also disagrees, the whole game moved to the new table (edit the enum).
    /// </summary>
    [LiveSiteFact]
    public async Task Phoenix1_grades_for_comparison_against_our_thresholds()
    {
        var ct = CancellationToken.None;
        Directory.CreateDirectory(DumpDir);
        using var pub = new HttpClient();
        pub.DefaultRequestHeaders.Add("Origin", "https://phoenix.piugame.com");

        var boardIds = new List<string>();
        foreach (var listUrl in new[]
                 {
                     "https://phoenix.piugame.com/leaderboard/over_ranking.php?lv=20",
                     "https://phoenix.piugame.com/leaderboard/over_ranking.php?lv=21"
                 })
        {
            var (status, listHtml, finalUrl) = await FetchTolerant(pub, listUrl, ct);
            _output.WriteLine($"P1 list {listUrl}: HTTP {status}, final={finalUrl}, {listHtml.Length} chars");
            boardIds.AddRange(Regex.Matches(listHtml, @"over_ranking_view\.php\?no=([a-zA-Z0-9+/=%]+)")
                .Select(m => m.Groups[1].Value).Distinct());
            if (boardIds.Count >= 4) break;
        }

        if (boardIds.Count == 0)
        {
            _output.WriteLine("=> Phoenix 1 served no anonymous board ids (it now gates boards too). " +
                              "Re-run authenticated to compare P1, or treat the P2 finding as game-wide pending that check.");
            return;
        }

        var observations = new List<Observation>();
        foreach (var boardId in boardIds.Distinct().Take(4))
        {
            var (status, html, _) = await FetchTolerant(pub,
                $"https://phoenix.piugame.com/leaderboard/over_ranking_view.php?no={boardId}", ct);
            await File.WriteAllTextAsync(Path.Combine(DumpDir, $"p1_board_{Sanitize(boardId)}.html"), html, ct);
            _output.WriteLine($"P1 board {boardId}: HTTP {status}");
            observations.AddRange(ExtractRows(html, "//div[contains(@class,'rangking_list_w')]//li", $"p1_{Sanitize(boardId)}"));
        }

        _output.WriteLine($"Phoenix 1: {observations.Count} graded rows from {boardIds.Distinct().Take(4).Count()} boards");
        var mismatches = observations.Where(o => PhoenixScore.From(o.Score).LetterGradeFor(MixEnum.Phoenix) != o.SiteGrade)
            .OrderBy(o => o.Score).ToList();
        _output.WriteLine($"Phoenix 1: {mismatches.Count} of {observations.Count} rows disagree with our thresholds");
        foreach (var m in mismatches.Take(30))
            _output.WriteLine(
                $"  score {m.Score,10:N0}: site='{m.SiteGrade.GetName()}' ours='{PhoenixScore.From(m.Score).LetterGradeFor(MixEnum.Phoenix).GetName()}'");
        _output.WriteLine(mismatches.Count == 0
            ? "=> Phoenix 1 STILL MATCHES our thresholds: the new table is Phoenix-2-only (per-mix needed)."
            : "=> Phoenix 1 ALSO disagrees: the whole game moved to the new grade table.");
    }

    /// <summary>
    ///     Low-end probe. Hard charts (level 24/25) have players scoring well below 900k, so their
    ///     boards expose the A / B / C floors that the crowded level-20 boards never reach. Reports
    ///     every row the site grades below AA so the sub-900k half of the new P2 table can be pinned.
    /// </summary>
    [LiveSiteFact]
    public async Task Phoenix2_low_end_grades_from_hard_charts()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        Directory.CreateDirectory(DumpDir);

        // The sparse HARDEST boards (lv 27/28) are where crawling reaches lowest — their weakest
        // qualifying scores sit around B (~781k). NOTE: over_ranking boards are top-heavy — popular
        // mid-level boards stay above ~880k even 40 pages / 15k rows deep — so C/D/F are NOT
        // reachable by crawling; the sub-B table needs the authoritative in-game/game-info source.
        var boardIds = new List<string>();
        foreach (var lv in new[] { 27, 26, 28, 25 })
        {
            var listHtml = await Fetch(client, $"https://piugame.com/leaderboard/over_ranking.php?lv={lv}", ct);
            boardIds.AddRange(Regex.Matches(listHtml, @"over_ranking_view\.php\?no=([a-zA-Z0-9+/=%]+)")
                .Select(m => m.Groups[1].Value).Distinct());
        }

        var observations = new List<Observation>();
        foreach (var boardId in boardIds.Distinct().Take(16))
        for (var page = 1; page <= 3; page++)
        {
            var html = await Fetch(client,
                $"https://piugame.com/leaderboard/over_ranking_view.php?no={boardId}&page={page}", ct);
            var rows = ExtractRows(html, "//div[contains(@class,'rangking_list_w')]//li", $"hard_{Sanitize(boardId)}_p{page}");
            if (rows.Count == 0) break;
            observations.AddRange(rows);
        }

        _output.WriteLine($"low-end rows: {observations.Count}, lowest score observed: {(observations.Count == 0 ? 0 : observations.Min(o => o.Score)):N0}");

        _output.WriteLine("");
        _output.WriteLine("=== Observed score range per SITE grade (low end) ===");
        _output.WriteLine($"{"grade",-6} {"count",6} {"min",10} {"max",10}   our range");
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
        {
            var forGrade = observations.Where(o => o.SiteGrade == grade).ToList();
            if (forGrade.Count == 0) continue;
            _output.WriteLine(
                $"{grade.GetName(),-6} {forGrade.Count,6} {forGrade.Min(o => o.Score),10:N0} {forGrade.Max(o => o.Score),10:N0}   " +
                $"[{(int)grade.GetMinimumScoreFor(MixEnum.Phoenix2):N0} .. {(int)grade.GetMaximumScoreFor(MixEnum.Phoenix2):N0}]");
        }

        _output.WriteLine("");
        _output.WriteLine("=== Every row the SITE grades below A (B/C/D/F — score asc) ===");
        foreach (var o in observations.Where(o => o.SiteGrade < PhoenixLetterGrade.A).OrderBy(o => o.Score))
            _output.WriteLine(
                $"  score {o.Score,10:N0}: site='{o.SiteGrade.GetName()}' ours='{PhoenixScore.From(o.Score).LetterGradeFor(MixEnum.Phoenix2).GetName()}'");

        Assert.NotEmpty(observations);
    }

    [LiveSiteFact]
    public async Task Phoenix2_grade_thresholds_observed_from_live_scores()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        Directory.CreateDirectory(DumpDir);

        var observations = new List<Observation>();

        // Source A — the service account's own best scores. Guaranteed grade+score per card,
        // but only covers whatever range this account has played.
        for (var page = 1; page <= 3; page++)
        {
            var url = $"https://piugame.com/my_page/my_best_score.php?&&page={page}";
            var html = await Fetch(client, url, ct);
            await File.WriteAllTextAsync(Path.Combine(DumpDir, $"best_scores_p{page}.html"), html, ct);
            var found = ExtractRows(html,
                "//ul[contains(@class,'recently_playeList')]/li | //ul[contains(@class,'my_best_scoreList')]/li",
                $"best_p{page}");
            observations.AddRange(found);
            _output.WriteLine($"best-scores page {page}: {found.Count} graded rows");
        }

        // Source B — busy per-song boards. A crowded level-20 board spans the whole grade
        // spectrum on one page, so a handful of boards pins every boundary at once — IF the
        // board rows carry grade images (reported below so we know either way).
        var boardIds = await CollectBoardIds(client, ct);
        _output.WriteLine($"collected {boardIds.Count} board ids");
        foreach (var boardId in boardIds.Take(12))
        {
            var url = $"https://piugame.com/leaderboard/over_ranking_view.php?no={boardId}";
            var html = await Fetch(client, url, ct);
            await File.WriteAllTextAsync(Path.Combine(DumpDir, $"board_{Sanitize(boardId)}.html"), html, ct);
            var found = ExtractRows(html, "//div[contains(@class,'rangking_list_w')]//li", $"board_{Sanitize(boardId)}");
            observations.AddRange(found);
            _output.WriteLine($"board {boardId}: {found.Count} graded rows");
        }

        // Source C — the authoritative grade table on the login-gated game-info page. Dump it
        // so the exact official boundary numbers (if the page lists them) can be read straight
        // from the source instead of inferred from observed score envelopes.
        foreach (var infoUrl in new[]
                 {
                     "https://piugame.com/game_info/basic_mode.php",
                     "https://piugame.com/game_info/rank_mode.php"
                 })
            try
            {
                var infoHtml = await Fetch(client, infoUrl, ct);
                var file = $"gameinfo_{Sanitize(infoUrl.Split('/').Last())}.html";
                await File.WriteAllTextAsync(Path.Combine(DumpDir, file), infoHtml, ct);
                var gradeRefs = GradeStemRegex.Matches(infoHtml).Select(m => m.Groups[1].Value).Distinct().Count();
                _output.WriteLine($"game-info {infoUrl}: {infoHtml.Length} chars, {gradeRefs} distinct grade images -> {file}");
            }
            catch (Exception e)
            {
                _output.WriteLine($"game-info {infoUrl}: fetch failed ({e.Message})");
            }

        _output.WriteLine("");
        Assert.NotEmpty(observations); // No graded rows anywhere = markup drift; investigate the dumps.

        // ---- Per-grade observed score envelope, ordered by grade ----
        _output.WriteLine("=== Observed score range per SITE grade (Phoenix 2) ===");
        _output.WriteLine($"{"grade",-6} {"count",6} {"min",10} {"max",10}   our code's range for that grade");
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
        {
            var forGrade = observations.Where(o => o.SiteGrade == grade).ToList();
            if (forGrade.Count == 0) continue;
            var ourMin = (int)grade.GetMinimumScoreFor(MixEnum.Phoenix2);
            var ourMax = (int)grade.GetMaximumScoreFor(MixEnum.Phoenix2);
            _output.WriteLine(
                $"{grade.GetName(),-6} {forGrade.Count,6} {forGrade.Min(o => o.Score),10:N0} {forGrade.Max(o => o.Score),10:N0}   " +
                $"[{ourMin:N0} .. {ourMax:N0}]");
        }

        // ---- Disagreements: the site says one grade, our thresholds say another ----
        var mismatches = observations
            .Where(o => PhoenixScore.From(o.Score).LetterGradeFor(MixEnum.Phoenix2) != o.SiteGrade)
            .OrderBy(o => o.Score)
            .ToList();

        _output.WriteLine("");
        _output.WriteLine($"=== {mismatches.Count} of {observations.Count} rows disagree with our current thresholds ===");
        foreach (var m in mismatches.Take(60))
            _output.WriteLine(
                $"  score {m.Score,10:N0}: site='{m.SiteGrade.GetName()}' ours='{PhoenixScore.From(m.Score).LetterGradeFor(MixEnum.Phoenix2).GetName()}'  ({m.Source})");
        if (mismatches.Count > 60) _output.WriteLine($"  ... and {mismatches.Count - 60} more");

        // ---- Inferred boundaries: lowest score seen at each grade (the grade's floor) ----
        _output.WriteLine("");
        _output.WriteLine("=== Inferred grade FLOOR (lowest score observed at each grade) ===");
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
        {
            var forGrade = observations.Where(o => o.SiteGrade == grade).ToList();
            if (forGrade.Count == 0) continue;
            var observedFloor = forGrade.Min(o => o.Score);
            var ourFloor = (int)grade.GetMinimumScoreFor(MixEnum.Phoenix2);
            var flag = observedFloor < ourFloor ? "  <-- observed BELOW our floor (floor moved down or grade shifted)" : "";
            _output.WriteLine($"{grade.GetName(),-6} observed-floor {observedFloor,10:N0}   our-floor {ourFloor,10:N0}{flag}");
        }

        _output.WriteLine("");
        _output.WriteLine($"Raw page dumps: {DumpDir}");
    }

    private static IReadOnlyList<Observation> ExtractRows(string html, string rowXPath, string source)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var rows = doc.DocumentNode.SelectNodes(rowXPath);
        var result = new List<Observation>();
        if (rows == null) return result;

        foreach (var row in rows)
        {
            var inner = row.InnerHtml;
            var gradeMatch = GradeStemRegex.Match(inner);
            if (!gradeMatch.Success) continue;
            var stem = gradeMatch.Groups[1].Value.ToLowerInvariant();
            if (!GradeByStem.TryGetValue(stem, out var grade)) continue;

            // The score is the largest 4+ digit number in the row's visible text (avatar urls
            // and image dimensions are in attributes, not InnerText).
            var text = HtmlEntity.DeEntitize(row.InnerText ?? "");
            var best = 0;
            foreach (Match m in ScoreDigitsRegex.Matches(text))
                if (int.TryParse(m.Value.Replace(",", ""), out var n) && n is > 0 and <= 1_000_000 && n > best)
                    best = n;
            if (best == 0) continue;

            result.Add(new Observation(best, stem, grade, source));
        }

        return result;
    }

    private async Task<IReadOnlyList<string>> CollectBoardIds(HttpClient client, CancellationToken ct)
    {
        var ids = new List<string>();
        foreach (var listUrl in new[]
                 {
                     "https://piugame.com/leaderboard/over_ranking.php?lv=20",
                     "https://piugame.com/leaderboard/over_ranking.php?lv=21",
                     "https://piugame.com/leaderboard/over_ranking.php"
                 })
        {
            var html = await Fetch(client, listUrl, ct);
            var found = Regex
                .Matches(html, @"over_ranking_view\.php\?no=([a-zA-Z0-9+/=%]+)")
                .Select(m => m.Groups[1].Value)
                .Distinct();
            ids.AddRange(found);
            if (ids.Count >= 6) break;
        }

        return ids.Distinct().ToList();
    }

    private static async Task<string> Fetch(HttpClient client, string url, CancellationToken ct)
    {
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static async Task<(int Status, string Body, string? FinalUrl)> FetchTolerant(
        HttpClient client, string url, CancellationToken ct)
    {
        var response = await client.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return ((int)response.StatusCode, body, response.RequestMessage?.RequestUri?.ToString());
    }

    private static string Sanitize(string raw)
    {
        var sb = new StringBuilder();
        foreach (var c in raw)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }
}
