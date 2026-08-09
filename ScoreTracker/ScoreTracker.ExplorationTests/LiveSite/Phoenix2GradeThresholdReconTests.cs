using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Empirical recon for the Phoenix 2 letter-grade thresholds. The official pages render a
///     grade image (<c>/l_img/grade/{stem}.png</c>) next to each raw score, so pairing the two
///     across a busy board (which spans ~700k–1,000,000 in a single page) pins every grade
///     boundary the site is actually using. For each observed (score, site-grade) pair this
///     also computes our own code's grade via <see cref="PhoenixLetterGradeHelperMethods.LetterGradeFor" /> and reports
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

    private sealed record LowRowTarget(string Player, string Song, string TypeStem, int Level, int MirrorScore,
        int MirrorPlace);

    // The complete set of sub-800k rows in the mirrored 2026-07-19 P2 snapshot (7 B-band, 1
    // C-band across all 1,434 boards' top-300) — the only live rows that can pin the B and C
    // floors, and 799,815 brackets the A floor from below.
    //
    // Refreshed from snapshot 9 (2026-08-02): the first three are the ONLY sub-500k rows that
    // have ever appeared in the mirror, all on Gargoyle - FULL SONG - S21 at places 251–253.
    // ⚠ A chart board holds TOP 300 and EVICTS as it fills — by 2026-08-08 that board had
    // reached 300 entries and its cutline had risen to 888,407, so these three are already gone
    // from the live site. They are kept as targets because "not found" is the informative
    // result: it dates the eviction, and it is why the mirror snapshot is the only durable
    // record of a low score. Boards still far from the 300 cap (Gargoyle D20 held 86 rows) keep
    // their low rows visible, which is where the B floor can still be read.
    private static readonly LowRowTarget[] LowRowTargets =
    {
        new("SOUND#1770", "Gargoyle", "s", 21, 448852, 253),
        new("HC#7045", "Gargoyle", "s", 21, 476838, 252),
        new("XVIRUSX#2791", "Gargoyle", "s", 21, 482128, 251),
        new("SCARFACE#5159", "Gargoyle", "s", 21, 690647, 83),
        new("HAIRDA100#4445", "Gargoyle", "d", 20, 707042, 38),
        new("BKRYU#8351", "Dead End", "s", 25, 781105, 5),
        new("HAIRDA100#4445", "Hyperion", "d", 20, 786427, 7),
        new("SUBAGAZE#2845", "Loki", "d", 20, 790956, 12),
        new("SILVERRABBIT#5827", "Freedom Dive", "s", 25, 793653, 31),
        new("KAIO#4193", "Big Daddy", "s", 21, 796609, 80),
        new("HAIRDA100#4445", "Gargoyle", "s", 22, 799815, 11)
    };

    private static readonly Regex ListTypeStemRegex =
        new(@"\/stepball\/full\/([a-zA-Z]+)_text\.png", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ListLevelDigitRegex =
        new(@"_num_([0-9])\.png", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    ///     Reads the site-displayed grade art for the eight known sub-800k board rows (found by
    ///     SQL over the mirror, which stores score+place but no grade). Each row is a direct
    ///     (score, official-grade) sample in the range where our floors are still guesses:
    ///     707,042 sits right on the guessed 700k B floor, and 799,815 brackets the 800k A
    ///     floor from below. Boards are found via the list's search parameter, and rows are
    ///     matched by profile name so an improved score still yields a (new) sample.
    /// </summary>
    [LiveSiteFact]
    public async Task Phoenix2_sub800k_board_rows_pin_the_low_grade_floors()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        Directory.CreateDirectory(DumpDir);

        var lowGradeSamples = new List<(string Who, string Chart, int Score, PhoenixLetterGrade Site)>();
        var found = 0;
        foreach (var target in LowRowTargets)
        {
            await Task.Delay(300, ct);
            var listHtml = await Fetch(client,
                $"https://piugame.com/leaderboard/over_ranking.php?lv=&search={Uri.EscapeDataString(target.Song)}",
                ct);
            var boardId = FindBoardId(listHtml, target);
            if (boardId == null)
            {
                _output.WriteLine($"[{target.Player}] {target.Song} {target.TypeStem.ToUpper()}{target.Level}: " +
                                  "board NOT FOUND via list search");
                continue;
            }

            // Learn rows-per-page from page 1, then jump to the mirror place's page (+/-1 —
            // live places drift as new scores land).
            await Task.Delay(300, ct);
            var page1 = await Fetch(client,
                $"https://piugame.com/leaderboard/over_ranking_view.php?no={boardId}&page=1", ct);
            var perPage = Math.Max(1, CountBoardRows(page1));
            var expectedPage = (target.MirrorPlace - 1) / perPage + 1;
            var hit = FindPlayerRow(page1, target.Player);
            foreach (var page in new[] { expectedPage, expectedPage + 1, expectedPage - 1, expectedPage + 2 })
            {
                if (hit != null || page <= 1 || page > 40) continue;
                await Task.Delay(300, ct);
                var html = await Fetch(client,
                    $"https://piugame.com/leaderboard/over_ranking_view.php?no={boardId}&page={page}", ct);
                hit = FindPlayerRow(html, target.Player);
            }

            var chartLabel = $"{target.Song} {target.TypeStem.ToUpper()}{target.Level}";
            if (hit == null)
            {
                _output.WriteLine($"[{target.Player}] {chartLabel}: row not found near place {target.MirrorPlace} " +
                                  $"(perPage {perPage}) — player may have climbed");
                continue;
            }

            found++;
            var (score, grade) = hit.Value;
            var ours = PhoenixScore.From(score).LetterGradeFor(MixEnum.Phoenix2);
            var drift = score == target.MirrorScore ? "" : $" (mirror had {target.MirrorScore:N0})";
            var verdict = grade == null ? "NO GRADE ART" : grade == ours ? "matches our floors" : "FLOOR MISMATCH";
            _output.WriteLine($"[{target.Player}] {chartLabel}: score {score:N0}{drift} site-grade " +
                              $"{grade?.GetName() ?? "?"} ours {ours.GetName()} -> {verdict}");
            if (grade != null && score < 840000) lowGradeSamples.Add((target.Player, chartLabel, score, grade.Value));
        }

        _output.WriteLine("");
        _output.WriteLine("=== Confirmed low-band (score, site-grade) samples ===");
        foreach (var s in lowGradeSamples.OrderBy(s => s.Score))
            _output.WriteLine($"  {s.Score,9:N0}  {s.Site.GetName(),-3}  {s.Who}  {s.Chart}");
        Assert.True(found >= 5,
            $"Only {found} of {LowRowTargets.Length} targeted sub-800k rows were located — the boards may have " +
            "shifted too much since the mirror snapshot; re-derive targets from a fresh import.");
    }

    /// <summary>
    ///     The deepest board in the game, read to the bottom. SQL over the mirror (snapshot 9,
    ///     2026-08-02) says <c>Gargoyle - FULL SONG - S21</c> is the ONLY Phoenix 2 chart board
    ///     that reaches below 700k — 255 rows down to 448,852, and every sub-500k row in the
    ///     entire mirror sits on it. Targeting individual known rows (as the sub800k fact does)
    ///     wastes that: crawling the whole board yields a dense (score, grade) sample across the
    ///     one score range where our floors are still guesses.
    ///     <para>
    ///         What it can settle: the D floor (500k) and C floor (600k) are owner-ratified
    ///         working values, unobservable when the deepest row anywhere was 690,647. A row that
    ///         renders D below 500,000 moves the floor; a row that renders F confirms the floor
    ///         sits above that score. Anything landing in the 482,128–690,647 gap pins both.
    ///     </para>
    /// </summary>
    [LiveSiteFact]
    public async Task Phoenix2_deepest_board_read_to_the_bottom_pins_the_low_floors()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        Directory.CreateDirectory(DumpDir);

        // Level 21 has only the FULL SONG cut, so type+level identifies the board unambiguously
        // among the eight Gargoyle boards.
        var probe = new LowRowTarget("", "Gargoyle", "s", 21, 0, 0);
        var listHtml = await Fetch(client,
            $"https://piugame.com/leaderboard/over_ranking.php?lv=&search={Uri.EscapeDataString(probe.Song)}", ct);
        var boardId = FindBoardId(listHtml, probe);
        Assert.True(boardId != null, "Could not locate the Gargoyle - FULL SONG - S21 board from the list search.");

        // ⚠ over_ranking_view.php IGNORES ?page= — it serves the whole board in one document
        // ("TOP 300" in its own header, and the markup carries no pagination links at all).
        // Learned 2026-08-08 by crawling 40 pages and getting the identical 300 rows every time;
        // stop as soon as a page repeats so this stays one request in practice.
        var observations = new List<Observation>();
        var seenPages = new HashSet<string>();
        for (var page = 1; page <= 40; page++)
        {
            await Task.Delay(300, ct);
            var html = await Fetch(client,
                $"https://piugame.com/leaderboard/over_ranking_view.php?no={boardId}&page={page}", ct);
            var rows = ExtractRows(html, "//div[contains(@class,'rangking_list_w')]//li", $"deepboard p{page}");
            if (rows.Count == 0)
            {
                _output.WriteLine($"page {page}: no rows — bottom of the board");
                break;
            }

            await File.WriteAllTextAsync(Path.Combine(DumpDir, $"p2_deepboard_p{page}.html"), html, ct);
            var fingerprint = string.Join(",", rows.Select(r => r.Score));
            if (!seenPages.Add(fingerprint))
            {
                _output.WriteLine($"page {page}: identical to a page already read — ?page= is ignored, stopping");
                break;
            }

            observations.AddRange(rows);
            _output.WriteLine($"page {page}: {rows.Count} rows, scores {rows.Min(r => r.Score):N0}–{rows.Max(r => r.Score):N0}");
        }

        Assert.NotEmpty(observations);
        _output.WriteLine("");
        _output.WriteLine($"=== {observations.Count} (score, grade) samples off one board ===");

        _output.WriteLine("");
        _output.WriteLine("lowest 30 rows, ascending — the boundary is eyeballable here:");
        foreach (var o in observations.OrderBy(o => o.Score).Take(30))
        {
            var ours = PhoenixScore.From(o.Score).LetterGradeFor(MixEnum.Phoenix2);
            _output.WriteLine($"  {o.Score,9:N0}  site {o.SiteGrade.GetName(),-4} ours {ours.GetName(),-4}" +
                              (o.SiteGrade == ours ? "" : "   <== FLOOR MISMATCH"));
        }

        _output.WriteLine("");
        _output.WriteLine($"{"grade",-6} {"observed min",13} {"observed max",13} {"our floor",11}  n");
        foreach (var g in observations.Select(o => o.SiteGrade).Distinct().OrderBy(g => g))
        {
            var band = observations.Where(o => o.SiteGrade == g).ToList();
            _output.WriteLine($"{g.GetName(),-6} {band.Min(o => o.Score),13:N0} {band.Max(o => o.Score),13:N0} " +
                              $"{(int)g.GetMinimumScoreFor(MixEnum.Phoenix2),11:N0}  {band.Count}");
        }

        // The floor a grade's lowest observed score implies, against the value we ship.
        _output.WriteLine("");
        var mismatches = observations
            .Where(o => o.SiteGrade != PhoenixScore.From(o.Score).LetterGradeFor(MixEnum.Phoenix2))
            .OrderBy(o => o.Score).ToList();
        foreach (var o in mismatches)
            _output.WriteLine($"MISMATCH {o.Score:N0}: site says {o.SiteGrade.GetName()}, " +
                              $"we say {PhoenixScore.From(o.Score).LetterGradeFor(MixEnum.Phoenix2).GetName()}");

        var below500 = observations.Where(o => o.Score < 500_000).ToList();
        _output.WriteLine("");
        _output.WriteLine(below500.Count == 0
            ? "[D floor] no sub-500k rows on this board today — the D floor stays a working value"
            : $"[D floor] {below500.Count} sub-500k rows, all rendering " +
              $"{string.Join("/", below500.Select(o => o.SiteGrade.GetName()).Distinct())} " +
              $"(lowest {below500.Min(o => o.Score):N0}) — " +
              (below500.All(o => o.SiteGrade == PhoenixLetterGrade.F)
                  ? "F, so the D floor is ABOVE " + below500.Max(o => o.Score).ToString("N0")
                  : "NOT all F, so the D floor is at or BELOW " + below500.Max(o => o.Score).ToString("N0")));

        Assert.True(mismatches.Count == 0,
            $"{mismatches.Count} of {observations.Count} board rows disagree with our Phoenix 2 grade floors — " +
            $"listed above, lowest first. This is the instrument reporting a real threshold, not a flake: " +
            $"the site's own grade art is truth. Dumps in {DumpDir}.");
        _output.WriteLine("");
        _output.WriteLine($"every one of {observations.Count} rows agrees with our floors");
    }

    private static string? FindBoardId(string listHtml, LowRowTarget target)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(listHtml);
        var candidates = doc.DocumentNode.SelectNodes("//li[.//a or @onclick]") ?? new HtmlNodeCollection(null);
        foreach (var li in candidates)
        {
            var inner = li.InnerHtml;
            var idMatch = Regex.Match(inner, @"over_ranking_view\.php\?no=([a-zA-Z0-9+/=%]+)");
            if (!idMatch.Success) continue;
            var typeMatch = ListTypeStemRegex.Match(inner);
            if (!typeMatch.Success ||
                !typeMatch.Groups[1].Value.Equals(target.TypeStem, StringComparison.OrdinalIgnoreCase)) continue;
            var digits = string.Join("",
                ListLevelDigitRegex.Matches(inner).Select(m => m.Groups[1].Value));
            if (digits.Length == 0 || int.Parse(digits) != target.Level) continue;
            return idMatch.Groups[1].Value;
        }

        return null;
    }

    private static int CountBoardRows(string boardHtml)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(boardHtml);
        return doc.DocumentNode.SelectNodes("//div[contains(@class,'rangking_list_w')]//li")?.Count ?? 0;
    }

    private static (int Score, PhoenixLetterGrade? Grade)? FindPlayerRow(string boardHtml, string player)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(boardHtml);
        var rows = doc.DocumentNode.SelectNodes("//div[contains(@class,'rangking_list_w')]//li");
        if (rows == null) return null;
        var wanted = Regex.Replace(player, @"\s+", "");
        foreach (var row in rows)
        {
            var nameNodes = row.SelectNodes(".//div[contains(@class,'profile_name')]");
            if (nameNodes == null) continue;
            var name = Regex.Replace(string.Join("", nameNodes.Select(n => n.InnerText)), @"\s+", "");
            if (!string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase)) continue;

            var text = HtmlEntity.DeEntitize(row.InnerText ?? "");
            var best = 0;
            foreach (Match m in ScoreDigitsRegex.Matches(text))
                if (int.TryParse(m.Value.Replace(",", ""), out var n) && n is > 0 and <= 1_000_000 && n > best)
                    best = n;
            if (best == 0) return null;

            var gradeMatch = GradeStemRegex.Match(row.InnerHtml);
            PhoenixLetterGrade? grade =
                gradeMatch.Success && GradeByStem.TryGetValue(gradeMatch.Groups[1].Value.ToLowerInvariant(), out var g)
                    ? g
                    : null;
            return (best, grade);
        }

        return null;
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
