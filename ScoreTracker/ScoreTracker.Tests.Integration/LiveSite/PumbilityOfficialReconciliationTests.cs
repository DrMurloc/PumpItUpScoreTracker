using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit.Abstractions;

namespace ScoreTracker.Tests.Integration.LiveSite;

/// <summary>
///     Whole-account PUMBILITY reconciliation: crawls the PiuTest account's complete best-score
///     list from the official my_page, reads the account's official PUMBILITY (All / Singles /
///     Doubles) from the ranking board's "MY RANKING DATA" block, and checks that our production
///     formula reproduces the official numbers exactly. Because every parsed card carries the
///     site's own displayed grade and plate, the same crawl also answers the open formula
///     questions as a variant matrix:
///     <list type="bullet">
///         <item>are sub-level-10 clears included in the official total (level floor vs none),</item>
///         <item>do the site's displayed grades agree with our per-mix score→grade table,</item>
///         <item>are the extrapolated below-A+ grade multipliers right (when such grades contribute),</item>
///         <item>is there a hidden "+1 base for singles", and</item>
///         <item>do singles use their own UG/EG/RG plate bonuses (community-claimed) or the shared table.</item>
///     </list>
///     An unexplained residual fails the test — that is the instrument telling us an assumption
///     broke. Raw pages and a per-card CSV land in %TEMP%\pumbility-recon (PIU_RECON_DUMP_DIR
///     overrides) for offline analysis.
///     <para>
///         Board quirks learned on the first run (2026-07-19): Phoenix 1's board ignores the
///         ?t= tab parameter (no per-type officials exist there) and displays integers; the
///         Phoenix 2 board credits an account via a daily batch (its own explainer says
///         "updated daily at xx:yy (GMT+09)" — verbatim, Andamiro left the placeholder), so a
///         freshly-played account reads rank '-' / 0.00 until the batch runs → INCONCLUSIVE,
///         not a formula verdict.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class PumbilityOfficialReconciliationTests : IClassFixture<PiuGameSessionFixture>
{
    // Officials render two decimals; per-chart values are Base×(grade+plate) with three exact
    // decimals, so even a floor-per-chart-at-2-decimals site implementation drifts ≤ 0.25 over
    // a 50-chart pool. The smallest real modelling error (one plate step on a low-level chart)
    // is ≥ ~0.27, so 0.5 separates rounding from wrong-formula. Phoenix 1's per-level bases are
    // an order of magnitude larger, so it gets a little more slack.
    private const double Phoenix2Tolerance = 0.5;
    private const double PhoenixTolerance = 2.0;
    private const int MaxBestScorePages = 300;
    private const int MaxBoardFallbackPages = 20;
    private static readonly TimeSpan Politeness = TimeSpan.FromMilliseconds(300);

    private static readonly string DumpDir =
        Environment.GetEnvironmentVariable("PIU_RECON_DUMP_DIR")
        ?? Path.Combine(Path.GetTempPath(), "pumbility-recon");

    // Card-image stems, matching the shapes PiuGameApi pins with approval fixtures: type/level
    // digits under /stepball/full/, plate under /plate/, grade under /grade/ (P2 adds a p2/ hop).
    private static readonly Regex TypeStemRegex =
        new(@"\/stepball\/full\/([a-zA-Z]+)_text\.png", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LevelDigitRegex =
        new(@"_num_([0-9])\.png", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PlateStemRegex =
        new(@"\/plate\/([a-zA-Z]+)\.png", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GradeStemRegex =
        new(@"\/grade\/([a-z_]+)\.png", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Scores render comma-grouped ("987,654"); requiring the comma group (or a 6+ digit run)
    // keeps numeric song titles like "1948" out of the fallback text scan.
    private static readonly Regex ScoreTextRegex =
        new(@"\d{1,3}(?:,\d{3})+|\d{6,7}", RegexOptions.Compiled);

    private static readonly Regex RecordedAtRegex =
        new(@"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\s*\(GMT([+-]\d{1,2})\)", RegexOptions.Compiled);

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

    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PumbilityOfficialReconciliationTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private sealed record BestCard(string Song, ChartType Type, int Level, int Score, PhoenixPlate? Plate,
        bool IsBroken, PhoenixLetterGrade? SiteGrade, DateTimeOffset? RecordedAt);

    private sealed record Officials(double? All, double? Singles, double? Doubles, string Rank, string CreditedAt,
        bool SpecTextPresent, string Notes);

    [LiveSiteFact]
    public async Task Phoenix2_official_pumbility_reconciles_with_our_formula_for_this_account()
    {
        var ct = CancellationToken.None;
        Directory.CreateDirectory(DumpDir);
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        var account = await _fixture.Api.GetAccountData(MixEnum.Phoenix2, client, ct);
        _output.WriteLine($"account: {account.AccountName}");

        var pages = await CrawlBestScores(client, "https://piugame.com", "p2", ct);
        var cards = pages.SelectMany(p => p).ToList();
        Assert.NotEmpty(cards);
        Summarize(cards);
        DumpCsv("p2", cards,
            c => Production2(c).ToString("F3", CultureInfo.InvariantCulture));

        await VerifyParserAgreement(MixEnum.Phoenix2, client, pages[0], ct);

        var officials = await ReadOfficials(client, "https://piugame.com", MixEnum.Phoenix2,
            account.AccountName.ToString(), ct);
        _output.WriteLine("");
        _output.WriteLine($"official PUMBILITY — All: {Fmt(officials.All)}  Singles: {Fmt(officials.Singles)}  " +
                          $"Doubles: {Fmt(officials.Doubles)}  rank: {officials.Rank}  " +
                          $"credited: {officials.CreditedAt}   ({officials.Notes})");
        _output.WriteLine(officials.SpecTextPresent
            ? "official spec text: \"sum of the top 50 highest-rated songs\" present — aggregation wording unchanged"
            : "official spec text MISSING from the board page — the site may have changed the PUMBILITY definition");

        // The title page's [S]/[D]/masked-tier ladders render the SAME pools live (verified
        // 2026-07-19: they moved with plays the daily board batch had not credited yet), so
        // they beat the board as officials whenever present. Total-from-title assumes the
        // masked tier gates on the merged pool — equal to S+D only while the account has ≤50
        // eligible charts.
        var ladder = await ReadTitleLadder(client, ct);
        _output.WriteLine($"title-page ladders (live) — Total: {Fmt(ladder.Total)}  [S]: {Fmt(ladder.S)}  " +
                          $"[D]: {Fmt(ladder.D)}");
        var officialsSource = ladder is { S: not null, D: not null } ? "title-page (live)" : "board (daily batch)";
        var effective = officials with
        {
            All = (officials.All is > 0 ? officials.All : null) ?? (ladder.Total is > 0 ? ladder.Total : null)
                  ?? officials.All,
            Singles = ladder.S ?? officials.Singles,
            Doubles = ladder.D ?? officials.Doubles,
            Notes = $"{officialsSource}; board: {officials.Notes}"
        };
        officials = effective;
        _output.WriteLine($"reconciling against: All {Fmt(officials.All)} / S {Fmt(officials.Singles)} / " +
                          $"D {Fmt(officials.Doubles)}   [{officialsSource}]");
        Assert.True(officials.All.HasValue || officials.Singles.HasValue || officials.Doubles.HasValue,
            $"Could not read any official PUMBILITY value (board MY RANKING DATA, first {MaxBoardFallbackPages} " +
            $"board pages, or title-page ladders). Inspect the dumps in {DumpDir}.");

        // ---- Variant matrix: named contribution models × (no floor | level>=10 floor) ----
        var cfg = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        PhoenixLetterGrade SiteOrOurs(BestCard c) =>
            c.SiteGrade ?? PhoenixScore.From(c.Score).LetterGradeFor(MixEnum.Phoenix2);
        var variants = new (string Name, Func<BestCard, double> Rate)[]
        {
            ("production (as shipped)", Production2),
            ("site-displayed grades", c => Manual2(cfg, c, SiteOrOurs(c), false, false)),
            ("site grades, Base+1 singles", c => Manual2(cfg, c, SiteOrOurs(c), true, false)),
            ("site grades, singles plate tbl", c => Manual2(cfg, c, SiteOrOurs(c), false, true)),
            ("Phoenix-1 grade table", c => Manual2(cfg, c,
                PhoenixScore.From(c.Score).LetterGradeFor(MixEnum.Phoenix), false, false))
        };

        _output.WriteLine("");
        _output.WriteLine("=== Variant matrix (ours − official; blank official ⇒ no Δ) ===");
        _output.WriteLine($"{"variant",-32} {"floor",-7} {"All",14} {"ΔAll",10} {"ΔS",10} {"ΔD",10}");
        var rows = new List<(string Name, bool Floor, double All, double S, double D)>();
        foreach (var (name, rate) in variants)
            foreach (var floor in new[] { false, true })
            {
                var (all, s, d) = ComputePools(cards, rate, floor);
                rows.Add((name, floor, all, s, d));
                _output.WriteLine(
                    $"{name,-32} {(floor ? "lvl≥10" : "none"),-7} {all,14:N2} {Delta(all, officials.All),10} " +
                    $"{Delta(s, officials.Singles),10} {Delta(d, officials.Doubles),10}");
            }

        PrintFindings(cards, officials, cfg, rows, Phoenix2Tolerance);

        // A brand-new account sits at rank '-' / 0.00 until the site's PUMBILITY batch first
        // credits it (the my_page explainer: "The Pumbility Ranking changes every day at 01:00
        // (GMT+9)") — that is an uncredited official value, not a formula verdict either way.
        if (officials.All == 0 && (officials.Singles ?? 0) == 0 && (officials.Doubles ?? 0) == 0 &&
            cards.Any(c => !c.IsBroken && c.Type is ChartType.Single or ChartType.Double))
        {
            _output.WriteLine("");
            _output.WriteLine("INCONCLUSIVE: the board shows rank '-' / 0.00 for this account despite crawled " +
                              "clears. The official batch runs daily at 01:00 (GMT+9); if 0.00 persists across " +
                              "batches, first-time crediting has an unknown gate (minimum chart count? mode?) — " +
                              "the formula and sub-10 verdicts wait either way.");
            return;
        }

        // The hard gate: the production formula must reproduce every official value we found,
        // under a single consistent floor rule.
        var productionArms = rows.Where(r => r.Name == variants[0].Name).ToList();
        var matchingArm = productionArms.Where(r =>
                Within(r.All, officials.All, Phoenix2Tolerance) &&
                Within(r.S, officials.Singles, Phoenix2Tolerance) &&
                Within(r.D, officials.Doubles, Phoenix2Tolerance))
            .Select(r => (bool?)r.Floor).FirstOrDefault();
        Assert.True(matchingArm != null,
            "Our production PUMBILITY formula does NOT reproduce the official values under either floor rule. " +
            $"Official All={Fmt(officials.All)} vs ours no-floor={productionArms[0].All:N2} / " +
            $"floor10={productionArms[1].All:N2} (S/D deltas above). An assumption broke — see the variant " +
            $"matrix and residual analysis in the output, and the dumps in {DumpDir}.");
        _output.WriteLine("");
        _output.WriteLine($"PRODUCTION FORMULA MATCH: floor rule = {(matchingArm == true ? "level>=10" : "none")}");
    }

    [LiveSiteFact]
    public async Task Phoenix_official_pumbility_reconciles_with_our_formula_for_this_account()
    {
        var ct = CancellationToken.None;
        Directory.CreateDirectory(DumpDir);
        var client = await _fixture.GetAuthenticatedClient(ct);
        var account = await _fixture.Api.GetAccountData(MixEnum.Phoenix, client, ct);
        _output.WriteLine($"account: {account.AccountName}");

        var pages = await CrawlBestScores(client, "https://phoenix.piugame.com", "p1", ct);
        var cards = pages.SelectMany(p => p).ToList();
        Assert.NotEmpty(cards);
        Summarize(cards);

        var cfg = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
        double Production(BestCard c) =>
            c.IsBroken ? 0 : cfg.GetScore(c.Type, DifficultyLevel.From(c.Level), PhoenixScore.From(c.Score));
        DumpCsv("p1", cards, c => Production(c).ToString("F3", CultureInfo.InvariantCulture));

        await VerifyParserAgreement(MixEnum.Phoenix, client, pages[0], ct);

        var officials = await ReadOfficials(client, "https://phoenix.piugame.com", MixEnum.Phoenix,
            account.AccountName.ToString(), ct);
        _output.WriteLine("");
        _output.WriteLine($"official PUMBILITY — All: {Fmt(officials.All)}  Singles: {Fmt(officials.Singles)}  " +
                          $"Doubles: {Fmt(officials.Doubles)}  rank: {officials.Rank}  " +
                          $"credited: {officials.CreditedAt}   ({officials.Notes})");
        if (!officials.All.HasValue)
        {
            _output.WriteLine("=> Account not readable on the Phoenix 1 board — skipping the P1 reconciliation " +
                              "(the P2 fact is the primary instrument).");
            return;
        }

        _output.WriteLine("");
        _output.WriteLine("=== Phoenix 1 (ours − official) ===");
        var arms = new List<(bool Floor, double All, double S, double D)>();
        foreach (var floor in new[] { false, true })
        {
            var (all, s, d) = ComputePools(cards, Production, floor);
            arms.Add((floor, all, s, d));
            _output.WriteLine($"{(floor ? "floor lvl≥10" : "no floor"),-14} All {all,14:N2} {Delta(all, officials.All),10} " +
                              $"ΔS {Delta(s, officials.Singles),10}  ΔD {Delta(d, officials.Doubles),10}");
        }

        // Grade-image drift canary comes free when the classic cards carry grade art.
        var graded = cards.Where(c => c.SiteGrade != null && !c.IsBroken).ToList();
        var disagreements = graded
            .Where(c => c.SiteGrade != PhoenixScore.From(c.Score).LetterGradeFor(MixEnum.Phoenix)).ToList();
        _output.WriteLine("");
        _output.WriteLine($"site-grade vs our P1 table: {disagreements.Count} disagreements / {graded.Count} graded cards");
        foreach (var c in disagreements.Take(20))
            _output.WriteLine($"  {c.Song} {TypeTag(c.Type)}{c.Level} {c.Score:N0}: site={c.SiteGrade!.Value.GetName()} " +
                              $"ours={PhoenixScore.From(c.Score).LetterGradeFor(MixEnum.Phoenix).GetName()}");

        PrintSub10Verdict(cards, officials,
            arms.Select(a => (a.Floor, a.All, a.S, a.D)).ToList(), PhoenixTolerance);

        var match = arms.Where(a =>
                Within(a.All, officials.All, PhoenixTolerance) &&
                Within(a.S, officials.Singles, PhoenixTolerance) &&
                Within(a.D, officials.Doubles, PhoenixTolerance))
            .Select(a => (bool?)a.Floor).FirstOrDefault();
        Assert.True(match != null,
            "Our Phoenix 1 PUMBILITY formula does NOT reproduce the official board values under either floor rule " +
            $"— official All={Fmt(officials.All)} vs ours no-floor={arms[0].All:N2} / floor10={arms[1].All:N2}. " +
            $"See output + dumps in {DumpDir}.");
        _output.WriteLine("");
        _output.WriteLine($"PRODUCTION FORMULA MATCH: floor rule = {(match == true ? "level>=10" : "none")}");
    }

    /// <summary>
    ///     Hunts for a PUMBILITY value the my_page renders directly — a potentially fresher (or
    ///     differently-gated) source than the ranking board's daily-batch MY RANKING DATA block,
    ///     which shows rank '-' / 0.00 for a freshly-played account. Output-only instrument: it
    ///     dumps each page and prints every visible-text neighbourhood of "pumbility" so a human
    ///     can spot a live value and the wording of any crediting rule.
    /// </summary>
    [LiveSiteFact]
    public async Task Phoenix2_my_page_pumbility_probe()
    {
        var ct = CancellationToken.None;
        Directory.CreateDirectory(DumpDir);
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        foreach (var path in new[]
                 {
                     "my_page", "my_page/play_data.php", "my_page/rival.php", "my_page/rank.php",
                     "my_page/title.php"
                 })
        {
            await Task.Delay(Politeness, ct);
            string html;
            try
            {
                html = await Fetch(client, $"https://piugame.com/{path}", ct);
            }
            catch (Exception e)
            {
                _output.WriteLine($"--- {path}: fetch failed ({e.Message})");
                continue;
            }

            var file = $"p2_probe_{Regex.Replace(path, "[^A-Za-z0-9]", "_")}.html";
            await File.WriteAllTextAsync(Path.Combine(DumpDir, file), html, ct);

            // Strip scripts/styles so the i18n translation blob doesn't drown the signal, then
            // print each remaining "pumbility" neighbourhood with its nearby numbers.
            var visible = Regex.Replace(html, @"<script[\s\S]*?</script>|<style[\s\S]*?</style>", "",
                RegexOptions.IgnoreCase);
            var hits = Regex.Matches(visible, @"[\s\S]{0,160}pumbility[\s\S]{0,240}", RegexOptions.IgnoreCase)
                .Select(m => Regex.Replace(HttpUtility.HtmlDecode(
                    Regex.Replace(m.Value, "<[^>]*>", " ")), @"\s+", " ").Trim())
                .Where(t => t.Length > 0)
                .Distinct()
                .Take(6)
                .ToList();
            _output.WriteLine($"--- {path}: {html.Length} chars → {file}");
            if (hits.Count == 0)
            {
                _output.WriteLine("    (no visible 'pumbility' text outside scripts)");
                continue;
            }

            foreach (var hit in hits) _output.WriteLine($"    {hit}");
        }

        // Every recent attempt (not just bests) — when a title-page pool disagrees with the
        // best-score computation, the difference is often a stale batch that captured an
        // earlier, lower attempt as the then-best. This is the data that resolves it.
        _output.WriteLine("");
        _output.WriteLine("=== recent plays (all attempts, newest first) ===");
        var recent = await _fixture.Api.GetRecentScores(MixEnum.Phoenix2, client, CancellationToken.None);
        foreach (var play in recent)
            _output.WriteLine($"  {play.RecordedAt:yyyy-MM-dd HH:mm zzz}  {play.SongName} {play.ChartType}" +
                              $" {play.Level}  score {(int)play.Score:N0}  plate {play.Plate.GetShorthand()}" +
                              $"  broken {play.IsBroken}");
    }

    // ---------- contribution models ----------

    private static double Production2(BestCard c)
    {
        var cfg = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        return cfg.GetScore(c.Type, DifficultyLevel.From(c.Level), PhoenixScore.From(c.Score),
            c.Plate ?? PhoenixPlate.RoughGame, c.IsBroken);
    }

    private static double Manual2(ScoringConfiguration cfg, BestCard c, PhoenixLetterGrade grade,
        bool plusOneSingles, bool singlesPlateTable)
    {
        if (c.IsBroken || c.Plate is null) return 0;
        if (c.Type is ChartType.CoOp or ChartType.SinglePerformance or ChartType.DoublePerformance) return 0;
        var baseRating = ScoringConfiguration.Phoenix2BaseRating(DifficultyLevel.From(c.Level))
                         + (plusOneSingles && c.Type == ChartType.Single ? 1 : 0);
        var gradeMultiplier = cfg.LetterGradeModifiers[grade];
        var plateBonus = cfg.PlateModifiers[c.Plate.Value];
        if (singlesPlateTable && c.Type == ChartType.Single)
            plateBonus = c.Plate.Value switch
            {
                PhoenixPlate.UltimateGame => 0.017,
                PhoenixPlate.ExtremeGame => 0.014,
                PhoenixPlate.RoughGame => -0.010,
                _ => plateBonus
            };
        return baseRating * (gradeMultiplier + plateBonus);
    }

    /// <summary>Merged / Singles / Doubles top-50 sums, mirroring PlayerRatingSaga's aggregation.</summary>
    private static (double All, double S, double D) ComputePools(IReadOnlyList<BestCard> cards,
        Func<BestCard, double> rate, bool floor10)
    {
        var rated = cards
            .Where(c => !c.IsBroken && c.Type != ChartType.CoOp)
            .Select(c => (Card: c, Rating: floor10 && c.Level < 10 ? 0.0 : rate(c)))
            .ToList();
        var all = rated.OrderByDescending(x => x.Rating).Take(50).Sum(x => x.Rating);
        var s = rated.Where(x => x.Card.Type == ChartType.Single)
            .OrderByDescending(x => x.Rating).Take(50).Sum(x => x.Rating);
        var d = rated.Where(x => x.Card.Type == ChartType.Double)
            .OrderByDescending(x => x.Rating).Take(50).Sum(x => x.Rating);
        return (all, s, d);
    }

    // ---------- findings ----------

    private void Summarize(IReadOnlyList<BestCard> cards)
    {
        _output.WriteLine("");
        _output.WriteLine($"crawled cards: {cards.Count}  " +
                          $"(S {cards.Count(c => c.Type == ChartType.Single)}, " +
                          $"D {cards.Count(c => c.Type == ChartType.Double)}, " +
                          $"CoOp {cards.Count(c => c.Type == ChartType.CoOp)}, " +
                          $"Perf {cards.Count(c => c.Type is ChartType.SinglePerformance or ChartType.DoublePerformance)}, " +
                          $"broken {cards.Count(c => c.IsBroken)}, sub-10 {cards.Count(c => c.Level < 10)})");
        var dated = cards.Where(c => c.RecordedAt != null).OrderByDescending(c => c.RecordedAt).Take(15).ToList();
        if (dated.Count == 0) return;
        _output.WriteLine("newest bests on the account (the fresh plays this run keys on):");
        foreach (var c in dated)
            _output.WriteLine($"  {c.RecordedAt:yyyy-MM-dd HH:mm zzz}  {c.Song} {TypeTag(c.Type)}{c.Level}  " +
                              $"{c.Score:N0} {(c.IsBroken ? "BROKEN" : c.Plate?.GetShorthand())} " +
                              $"{c.SiteGrade?.GetName() ?? "?"}");
    }

    private void PrintFindings(IReadOnlyList<BestCard> cards, Officials officials, ScoringConfiguration cfg,
        IReadOnlyList<(string Name, bool Floor, double All, double S, double D)> rows, double tolerance)
    {
        _output.WriteLine("");
        _output.WriteLine("=== Findings ===");

        // Score→grade table drift: the site's displayed grade is truth.
        var graded = cards.Where(c => c.SiteGrade != null && !c.IsBroken).ToList();
        var disagreements = graded
            .Where(c => c.SiteGrade != PhoenixScore.From(c.Score).LetterGradeFor(MixEnum.Phoenix2)).ToList();
        _output.WriteLine($"[grades] site grade vs our P2 table: {disagreements.Count} disagreements / {graded.Count} graded cards");
        foreach (var c in disagreements.Take(20))
            _output.WriteLine($"  {c.Song} {TypeTag(c.Type)}{c.Level} {c.Score:N0}: site={c.SiteGrade!.Value.GetName()} " +
                              $"ours={PhoenixScore.From(c.Score).LetterGradeFor(MixEnum.Phoenix2).GetName()}");

        // What the merged top-50 actually contains, and which open constants it exercises.
        var contributing = cards
            .Where(c => !c.IsBroken && c.Type != ChartType.CoOp)
            .Select(c => (Card: c, Rating: Production2(c)))
            .Where(x => x.Rating > 0)
            .OrderByDescending(x => x.Rating)
            .Take(50).ToList();
        var levels = contributing.Select(x => x.Card.Level).Distinct().OrderBy(l => l).ToList();
        _output.WriteLine($"[base]   contributing levels: {string.Join(",", levels)}" +
                          (levels.Any(l => l < 16) ? "  — extends Base(L) verification below the crawled 16–25 range" : ""));
        var belowAPlus = contributing.Where(x =>
            (x.Card.SiteGrade ?? PhoenixScore.From(x.Card.Score).LetterGradeFor(MixEnum.Phoenix2)) <
            PhoenixLetterGrade.APlus).ToList();
        _output.WriteLine(belowAPlus.Count == 0
            ? "[<A+]    no contributing cards below A+ — the extrapolated low-grade multipliers stay unverified"
            : $"[<A+]    {belowAPlus.Count} contributing cards below A+ — a total match CONFIRMS the extrapolated multipliers for: " +
              string.Join(", ", belowAPlus.GroupBy(x =>
                      x.Card.SiteGrade ?? PhoenixScore.From(x.Card.Score).LetterGradeFor(MixEnum.Phoenix2))
                  .OrderBy(g => g.Key)
                  .Select(g => $"{g.Key.GetName()} ({cfg.LetterGradeModifiers[g.Key]:F2}) ×{g.Count()}")));
        var perfect = contributing.Count(x => x.Card.Score == 1_000_000);
        if (perfect > 0)
            _output.WriteLine($"[PG]     {perfect} perfect 1,000,000 cards contribute — exercises SSS+ 1.50 + PG 0.020");

        // Structural: with ≤50 eligible charts the merged pool must equal S+D exactly.
        var eligible = cards.Count(c => !c.IsBroken && c.Type is ChartType.Single or ChartType.Double);
        if (officials is { All: not null, Singles: not null, Doubles: not null })
        {
            var sumDelta = officials.All.Value - (officials.Singles.Value + officials.Doubles.Value);
            _output.WriteLine($"[struct] eligible cards {eligible}: official All − (S+D) = {sumDelta:N2}" +
                              (eligible <= 50 ? " (≤50 charts ⇒ must be ~0 under the merged-top-50 model)" : ""));
        }

        PrintSub10Verdict(cards, officials,
            rows.Where(r => r.Name.StartsWith("production")).Select(r => (r.Floor, r.All, r.S, r.D)).ToList(),
            tolerance);

        // Hypothesis variants: called out only when this account's data can actually separate them.
        var baseline = rows.First(r => r.Name == "site-displayed grades" && !r.Floor);
        foreach (var name in new[] { "site grades, Base+1 singles", "site grades, singles plate tbl", "Phoenix-1 grade table" })
        {
            var variant = rows.First(r => r.Name == name && !r.Floor);
            var separation = Math.Abs(variant.All - baseline.All);
            if (separation < 0.005)
            {
                _output.WriteLine($"[variant] '{name}': indistinguishable on this account (no separating charts)");
                continue;
            }

            var verdict = officials.All == null ? "no official value"
                : Within(variant.All, officials.All, tolerance) && !Within(baseline.All, officials.All, tolerance)
                    ? "MATCHES official — baseline model is WRONG"
                    : !Within(variant.All, officials.All, tolerance) && Within(baseline.All, officials.All, tolerance)
                        ? "REJECTED by official total"
                        : "ambiguous";
            _output.WriteLine($"[variant] '{name}': separates by {separation:N2} → {verdict}");
        }

        // The one-sided signature: doubles reconciling while singles miss means the per-chart
        // core (Base, grades, plates) is right and a SINGLES-ONLY rule is unknown.
        if (officials is { Singles: not null, Doubles: not null })
        {
            var production = rows.First(r => r.Name.StartsWith("production") && !r.Floor);
            if (Math.Abs(production.D - officials.Doubles.Value) <= tolerance &&
                Math.Abs(production.S - officials.Singles.Value) > tolerance)
                _output.WriteLine($"[S≠D]    doubles reconcile EXACTLY (Δ {Delta(production.D, officials.Doubles)}) " +
                                  $"but singles do not (Δ {Delta(production.S, officials.Singles)}) — a singles-only " +
                                  "valuation rule is missing. Differential probe: play ONE single, re-run, and the " +
                                  "[S] ladder delta IS that chart's official value.");
        }

        // Residual bookkeeping for whichever arm gets closest — the number to explain when red.
        if (officials.All != null)
        {
            var best = rows.OrderBy(r => Math.Abs(r.All - officials.All.Value)).First();
            _output.WriteLine($"[residual] closest variant: '{best.Name}' ({(best.Floor ? "floor" : "no floor")}) " +
                              $"Δ = {best.All - officials.All.Value:+0.000;-0.000;0.000}");
        }
    }

    private void PrintSub10Verdict(IReadOnlyList<BestCard> cards, Officials officials,
        IReadOnlyList<(bool Floor, double All, double S, double D)> productionArms, double tolerance)
    {
        var sub10 = cards.Where(c => c.Level < 10 && !c.IsBroken && c.Type != ChartType.CoOp).ToList();
        if (sub10.Count == 0)
        {
            _output.WriteLine("[sub-10] the account has NO sub-level-10 clears — the floor question is untestable " +
                              "from this account; clear one sub-10 chart and re-run");
            return;
        }

        foreach (var c in sub10)
            _output.WriteLine($"[sub-10] candidate: {c.Song} {TypeTag(c.Type)}{c.Level} {c.Score:N0} " +
                              $"{c.Plate?.GetShorthand()} {c.SiteGrade?.GetName() ?? "?"}");
        var noFloor = productionArms.First(a => !a.Floor);
        var floored = productionArms.First(a => a.Floor);
        var separation = Math.Abs(noFloor.All - floored.All);
        if (separation < 0.005)
        {
            _output.WriteLine($"[sub-10] {sub10.Count} sub-10 clears exist but none land in the top-50 — " +
                              "inclusion cannot move the official total; question stays open on this account");
            return;
        }

        if (officials.All == null) return;
        var includedMatches = Within(noFloor.All, officials.All, tolerance);
        var excludedMatches = Within(floored.All, officials.All, tolerance);
        _output.WriteLine(includedMatches == excludedMatches
            ? $"[sub-10] AMBIGUOUS: included Δ={noFloor.All - officials.All.Value:N2}, " +
              $"excluded Δ={floored.All - officials.All.Value:N2}"
            : includedMatches
                ? $"[sub-10] ANSWER: sub-level-10 clears ARE INCLUDED in official PUMBILITY " +
                  $"(they move this account's total by {separation:N2} and the official value tracks them)"
                : "[sub-10] ANSWER: sub-level-10 clears are EXCLUDED from official PUMBILITY " +
                  $"(a level≥10 floor reproduces the official value; no-floor is off by {noFloor.All - officials.All.Value:N2})");
    }

    // ---------- crawling ----------

    private async Task<List<List<BestCard>>> CrawlBestScores(HttpClient client, string baseUrl, string tag,
        CancellationToken ct)
    {
        var pages = new List<List<BestCard>>();
        int? maxPage = null;
        for (var page = 1; page <= MaxBestScorePages && (maxPage == null || page <= maxPage); page++)
        {
            var html = await Fetch(client, $"{baseUrl}/my_page/my_best_score.php?&&page={page}", ct);
            if (page == 1)
                await File.WriteAllTextAsync(Path.Combine(DumpDir, $"{tag}_best_scores_p1.html"), html, ct);
            var document = new HtmlDocument();
            document.LoadHtml(html);
            maxPage ??= TryParseMaxPage(document);
            var lis = document.DocumentNode.SelectNodes("//ul[contains(@class,'recently_playeList')]/li")
                      ?? document.DocumentNode.SelectNodes("//ul[contains(@class,'my_best_scoreList')]/li");
            if (lis == null || lis.Count == 0) break;

            var cards = new List<BestCard>();
            foreach (var li in lis)
                try
                {
                    var card = ParseCard(li);
                    if (card != null) cards.Add(card);
                }
                catch (Exception e)
                {
                    _output.WriteLine($"card parse error on page {page}: {e.Message}");
                }

            pages.Add(cards);
            await Task.Delay(Politeness, ct);
        }

        _output.WriteLine($"crawled {pages.Count} best-score pages (site reports max {maxPage?.ToString() ?? "?"})");
        if (maxPage > pages.Count)
            _output.WriteLine($"WARNING: crawl truncated at {pages.Count}/{maxPage} pages — the list sorts " +
                              "newest-first, so an older top-50 contributor may be missing and the reconciliation " +
                              "would read LOW. Raise MaxBestScorePages for a complete read.");
        return pages;
    }

    private BestCard? ParseCard(HtmlNode li)
    {
        var typeUrl = li.SelectNodes(".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'tw')]//img")
            ?.First().GetAttributeValue("src", "");
        if (typeUrl == null) return null;
        if (typeUrl.Contains("u_text", StringComparison.OrdinalIgnoreCase)) return null; // UCS
        var typeMatch = TypeStemRegex.Match(typeUrl);
        // No _text stem at all is the API parser's SinglePerformance fallback — mirror it.
        ChartType? type = !typeMatch.Success
            ? ChartType.SinglePerformance
            : typeMatch.Groups[1].Value.ToLowerInvariant() switch
            {
                "c" => ChartType.CoOp,
                "s" => ChartType.Single,
                "d" => ChartType.Double,
                "sp" => ChartType.SinglePerformance,
                "dp" => ChartType.DoublePerformance,
                _ => null
            };
        if (type == null) return null;

        var digits = string.Join("", li.SelectNodes(".//div[contains(@class,'stepBall_img_wrap')]//img")
            ?.Select(i => LevelDigitRegex.Match(i.GetAttributeValue("src", "")))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value) ?? Enumerable.Empty<string>());
        var level = digits.Length == 0 ? 29 : int.Parse(digits, CultureInfo.InvariantCulture);

        var song = HttpUtility.HtmlDecode(
            li.SelectSingleNode(".//div[contains(@class,'song_name')]/p")?.InnerText
            ?? li.SelectNodes(".//div[contains(@class,'song_name')]")?.First().ChildNodes.First().InnerText
            ?? "?").Trim();

        int score;
        var scoreNode = li.SelectSingleNode(
            ".//div[contains(@class,'li_in') and contains(@class,'ac')]/i[contains(@class,'tx')]");
        if (scoreNode != null)
        {
            score = int.Parse(scoreNode.InnerText.Replace(",", "").Trim(), CultureInfo.InvariantCulture);
        }
        else
        {
            var text = HtmlEntity.DeEntitize(
                li.SelectSingleNode(".//div[contains(@class,'etc_con')]")?.InnerText ?? li.InnerText ?? "");
            var best = 0;
            foreach (Match m in ScoreTextRegex.Matches(text))
                if (int.TryParse(m.Value.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var n) && n is > 0 and <= 1_000_000 && n > best)
                    best = n;
            if (best == 0) return null;
            score = best;
        }

        var html = li.InnerHtml;
        PhoenixPlate? plate = null;
        var plateMatch = PlateStemRegex.Match(html);
        if (plateMatch.Success)
            try
            {
                plate = PhoenixPlateHelperMethods.ParseShorthand(plateMatch.Groups[1].Value);
            }
            catch (KeyNotFoundException)
            {
                _output.WriteLine($"unknown plate stem '{plateMatch.Groups[1].Value}' on {song} — card skipped");
                return null;
            }

        var gradeMatch = GradeStemRegex.Match(html);
        PhoenixLetterGrade? grade =
            gradeMatch.Success && GradeByStem.TryGetValue(gradeMatch.Groups[1].Value.ToLowerInvariant(), out var g)
                ? g
                : null;

        DateTimeOffset? recordedAt = null;
        var dateText = li.SelectSingleNode(".//p[contains(@class,'recently_date_tt')]")?.InnerText;
        if (dateText != null)
        {
            var m = RecordedAtRegex.Match(dateText);
            if (m.Success)
                recordedAt = new DateTimeOffset(
                    DateTime.ParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None),
                    TimeSpan.FromHours(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
        }

        return new BestCard(song, type.Value, level, score, plate, plate == null, grade, recordedAt);
    }

    /// <summary>
    ///     The production importer parser and this recon parser must read page 1 identically —
    ///     if they diverge, either the importer is dropping cards or this instrument is, and the
    ///     reconciliation cannot be trusted.
    /// </summary>
    private async Task VerifyParserAgreement(MixEnum mix, HttpClient client, IReadOnlyList<BestCard> myPage1,
        CancellationToken ct)
    {
        await Task.Delay(Politeness, ct);
        var dto = await _fixture.Api.GetBestScores(mix, client, 1, ct);
        string Key(string song, ChartType type, int level, int score, PhoenixPlate? plate, bool broken) =>
            $"{song}|{type}|{level}|{score}|{plate?.ToString() ?? "-"}|{broken}";
        var apiKeys = dto.Scores
            .Select(s => Key(s.SongName.ToString(), s.ChartType, (int)s.Level, (int)s.Score, s.Plate, s.IsBroken))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        var myKeys = myPage1
            .Select(c => Key(c.Song, c.Type, c.Level, c.Score, c.Plate, c.IsBroken))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
        var onlyApi = apiKeys.Except(myKeys).ToList();
        var onlyMine = myKeys.Except(apiKeys).ToList();
        foreach (var k in onlyApi) _output.WriteLine($"parser drift — importer-only card: {k}");
        foreach (var k in onlyMine) _output.WriteLine($"parser drift — recon-only card: {k}");
        Assert.True(onlyApi.Count == 0 && onlyMine.Count == 0,
            $"The importer parser and the recon parser disagree on page 1 ({onlyApi.Count} importer-only / " +
            $"{onlyMine.Count} recon-only cards — listed above). Fix the drift before trusting the reconciliation.");
        _output.WriteLine($"parser agreement: importer and recon read page 1 identically ({myKeys.Count} cards)");
    }

    private async Task<Officials> ReadOfficials(HttpClient client, string baseUrl, MixEnum mix, string accountName,
        CancellationToken ct)
    {
        var notes = new List<string>();
        var values = new double?[3];
        var rank = "?";
        var creditedAt = "?";
        var specTextPresent = false;
        string? allTabHtml = null;
        var tabs = new (ChartType? Type, string Tab, string Label)[]
            { (null, "", "All"), (ChartType.Single, "s", "Singles"), (ChartType.Double, "d", "Doubles") };
        for (var i = 0; i < tabs.Length; i++)
        {
            var (type, tab, label) = tabs[i];
            await Task.Delay(Politeness, ct);
            var html = await Fetch(client, $"{baseUrl}/leaderboard/pumbility_ranking.php?t={tab}&page=1", ct);
            await File.WriteAllTextAsync(
                Path.Combine(DumpDir, $"{(mix == MixEnum.Phoenix2 ? "p2" : "p1")}_board_{label}.html"), html, ct);
            if (i == 0)
            {
                allTabHtml = html;
                // Definition-drift tripwire: the board page embeds the official PUMBILITY
                // explainer; if the top-50 wording vanishes, the model may have changed.
                specTextPresent = html.Contains("top 50 highest-rated songs", StringComparison.OrdinalIgnoreCase);
            }
            else if (html == allTabHtml)
            {
                // Phoenix 1's board ignores ?t= and serves the All page for every tab — there
                // are no per-type officials there, only the merged value.
                notes.Add($"{label}: no per-type tab (board serves the All page)");
                continue;
            }

            var document = new HtmlDocument();
            document.LoadHtml(html);
            var mine = document.DocumentNode.SelectSingleNode("//div[contains(@class,'my_pumblitiy_wrap')]");
            var valueText = mine?.SelectSingleNode(".//div[contains(@class,'score')]//i[contains(@class,'tt')]")
                ?.InnerText;
            if (i == 0 && mine != null)
            {
                rank = mine.SelectSingleNode(".//div[contains(@class,'num')]//i[contains(@class,'tt')]")
                    ?.InnerText.Trim() ?? "?";
                creditedAt = mine.SelectSingleNode(".//div[contains(@class,'date')]//i[contains(@class,'tt')]")
                    ?.InnerText.Trim() ?? "(no date shown)";
            }

            if (valueText != null && double.TryParse(valueText.Replace(",", "").Trim(),
                    NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v))
            {
                values[i] = v;
                notes.Add($"{label}: MY RANKING DATA");
                continue;
            }

            // No MY block (markup drift, or the tab hides it) — walk the board for the name.
            for (var page = 1; page <= MaxBoardFallbackPages; page++)
            {
                var result = await _fixture.Api.GetPumbilityRankings(mix, type, page, client, ct);
                var hit = result.Entries.FirstOrDefault(e => NamesMatch(e.ProfileName, accountName));
                if (hit != null)
                {
                    values[i] = hit.Pumbility;
                    notes.Add($"{label}: board p{page}");
                    break;
                }

                if (result.IsEnd || result.Entries.Length == 0) break;
                await Task.Delay(Politeness, ct);
            }

            if (values[i] == null) notes.Add($"{label}: NOT FOUND");
        }

        return new Officials(values[0], values[1], values[2], rank, creditedAt, specTextPresent,
            string.Join("; ", notes));
    }

    /// <summary>
    ///     The live pool values off my_page/title.php: every pumbility-ladder title renders
    ///     "[ current / target ]", where current is the pool the ladder gates on — [S]/[D]
    ///     prefixes for the per-type pools, the masked "???" tier for the overall total.
    /// </summary>
    private async Task<(double? S, double? D, double? Total)> ReadTitleLadder(HttpClient client,
        CancellationToken ct)
    {
        await Task.Delay(Politeness, ct);
        var html = await Fetch(client, "https://piugame.com/my_page/title.php", ct);
        await File.WriteAllTextAsync(Path.Combine(DumpDir, "p2_title_ladder.html"), html, ct);
        double? s = null, d = null;
        // Multiple masked ("???") ladders exist and most sit at 0; the total-PUMBILITY one is
        // the masked ladder actually tracking a value, so keep the largest plausible one.
        var maskedValues = new List<double>();
        foreach (var segment in html.Split("data-name=\"").Skip(1))
        {
            var quote = segment.IndexOf('"');
            if (quote <= 0) continue;
            var name = segment[..quote];
            var bracket = Regex.Match(segment, @"\[ ?([0-9.,]+) ?/ ?([0-9.,]+) ?\]");
            if (!bracket.Success) continue;
            if (!double.TryParse(bracket.Groups[1].Value.Replace(",", ""), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var value)) continue;
            if (!double.TryParse(bracket.Groups[2].Value.Replace(",", ""), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var target) || target < 5000)
                continue; // pumbility ladders start at 5,000 — clear-count ladders stay below

            if (name.StartsWith("[S]", StringComparison.Ordinal)) s ??= value;
            else if (name.StartsWith("[D]", StringComparison.Ordinal)) d ??= value;
            else if (name.Length > 0 && name.All(ch => ch == '?') && target >= 10000 && value <= 25000)
                maskedValues.Add(value);
        }

        return (s, d, maskedValues.Count > 0 ? maskedValues.Max() : null);
    }

    // ---------- small helpers ----------

    private static bool NamesMatch(string a, string b)
    {
        static string Norm(string s) => Regex.Replace(s, @"\s+", "").Trim();
        return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Within(double ours, double? official, double tolerance)
    {
        return official == null || Math.Abs(ours - official.Value) <= tolerance;
    }

    private static string Delta(double ours, double? official)
    {
        return official == null ? "" : (ours - official.Value).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
    }

    private static string Fmt(double? value)
    {
        return value?.ToString("N2", CultureInfo.InvariantCulture) ?? "(not found)";
    }

    private static string TypeTag(ChartType type)
    {
        return type switch
        {
            ChartType.Single => "S",
            ChartType.Double => "D",
            ChartType.CoOp => "CoOp",
            ChartType.SinglePerformance => "SP",
            ChartType.DoublePerformance => "DP",
            _ => type.ToString()
        };
    }

    private static int? TryParseMaxPage(HtmlDocument document)
    {
        var lastI = document.DocumentNode.SelectNodes(".//i[contains(@class,'last')]")?.FirstOrDefault();
        var onclick = lastI?.ParentNode.GetAttributeValue("onclick", "");
        var parts = onclick?.Split('=');
        if (parts is not { Length: > 1 }) return null;
        return int.TryParse(parts[^1].Trim('\'', ')', ';', ' '), out var maxPage) ? maxPage : null;
    }

    private void DumpCsv(string tag, IReadOnlyList<BestCard> cards, Func<BestCard, string> contribution)
    {
        var path = Path.Combine(DumpDir, $"{tag}_cards.csv");
        var sb = new StringBuilder("song,type,level,score,site_grade,plate,broken,recorded_at,contribution\n");
        foreach (var c in cards)
            sb.Append('"').Append(c.Song.Replace("\"", "\"\"")).Append("\",")
                .Append(TypeTag(c.Type)).Append(',')
                .Append(c.Level).Append(',')
                .Append(c.Score).Append(',')
                .Append(c.SiteGrade?.GetName() ?? "").Append(',')
                .Append(c.Plate?.GetShorthand() ?? "").Append(',')
                .Append(c.IsBroken).Append(',')
                .Append(c.RecordedAt?.ToString("O") ?? "").Append(',')
                .Append(contribution(c)).Append('\n');
        File.WriteAllText(path, sb.ToString());
        _output.WriteLine($"per-card CSV: {path}");
    }

    private static async Task<string> Fetch(HttpClient client, string url, CancellationToken ct)
    {
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
