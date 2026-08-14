using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     A PUMBILITY tier is subdivided into unnamed "levels" — DIAMOND 1..5 and so on — that the site
///     states nowhere in text. The only carrier is the badge image beside a player's number on the
///     ranking board, <c>/l_img/pumbility/pumbility_NN.png</c>, whose index is the whole signal.
///     Two probes: one downloads every badge in the family (enumerating the URL pattern reaches rungs
///     no live player currently wears), the other crawls the board and brackets each worn badge to the
///     PB range of its wearers — a cutoff is pinned between one badge's highest wearer and the next
///     badge's lowest, so the crawl narrows an interval rather than reading a number.
///     Read-only (GETs of a login-gated ranking + static images). Run on demand:
///     <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/... --filter "FullyQualifiedName~Phoenix2PumbilityLevelBadgeRecon"</c>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Phoenix2PumbilityLevelBadgeReconTests : IClassFixture<PiuGameSessionFixture>
{
    private const int MaxPages = 40;
    private const int MaxBadgeIndex = 80;

    private static readonly string DownloadDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "pumbility-level-badges");

    // The named [P.B] gem thresholds we already hold, for anchoring an observed badge to a tier.
    private static readonly (string Name, int Threshold)[] GemLadder =
    [
        ("[P.B] BRONZE", 10000), ("[P.B] SILVER", 12500), ("[P.B] GOLD", 15000), ("[P.B] PLATINUM", 16000),
        ("[P.B] DIAMOND", 17000), ("[P.B] RED BERYL", 18000), ("[P.B] ALEXANDRITE", 19000),
        ("ABYSS ABSOLUTE", 20000)
    ];

    /// <summary>
    ///     The ladder read off the 2026-08-13 crawl: five levels per gem, evenly splitting that gem's
    ///     span, badge index running 1..36 across the whole ladder (so index = FirstBadge + level - 1,
    ///     and the numeral drawn on the art is the level), plus index 0 — a plain grey sphere with no
    ///     numeral — for a pool that has not reached BRONZE.
    ///     <para>
    ///         The STRUCTURE is observed end to end: every rung's art exists and numbers itself 1..5,
    ///         and the art family changes at exactly the indices ≡ 1 (mod 5) where the gem thresholds
    ///         sit. The THRESHOLDS are observed only from DIAMOND up — all 1,000 board rows agreed
    ///         and each cutoff fell in an interval containing exactly one round number. Below 17,000
    ///         no wearer is visible (the board bottoms out there), so BRONZE/SILVER/GOLD/PLATINUM
    ///         rest on the gems' own thresholds split five ways: 200 across a 1,000-wide gem, 500
    ///         across the 2,500-wide BRONZE and SILVER.
    ///     </para>
    ///     ⚠ Indices 0..9 are zero-padded in the URL (<c>pumbility_01.png</c>) and 10..36 are not.
    ///     Asking only the bare spelling makes the bottom half look unpublished, which is what it did.
    /// </summary>
    private static readonly (string Gem, int FirstBadge, int TierStart, int Step)[] DerivedLadder =
    [
        ("UNRANKED", 0, 0, 0), // below BRONZE — a blank grey sphere, no numeral
        ("[P.B] BRONZE", 1, 10000, 500), // thresholds derived; art observed
        ("[P.B] SILVER", 6, 12500, 500), // thresholds derived; art observed
        ("[P.B] GOLD", 11, 15000, 200), // thresholds derived; art observed
        ("[P.B] PLATINUM", 16, 16000, 200), // thresholds derived; art observed
        ("[P.B] DIAMOND", 21, 17000, 200), // observed (LV.1 start inherited from the gem threshold)
        ("[P.B] RED BERYL", 26, 18000, 200), // observed
        ("[P.B] ALEXANDRITE", 31, 19000, 200), // observed
        ("ABYSS ABSOLUTE", 36, 20000, 0) // observed — the capstone has no levels and draws no numeral
    ];

    /// <summary>Which badge <see cref="DerivedLadder" /> says a total-PUMBILITY pool should draw.</summary>
    private static int? PredictBadge(double pumbility)
    {
        foreach (var (_, first, start, step) in DerivedLadder.Reverse())
        {
            if (pumbility < start) continue;
            return step == 0 ? first : first + Math.Min(4, (int)((pumbility - start) / step));
        }

        return null;
    }

    /// <summary>
    ///     The rung a badge index sits on, or null when the index is off the ladder entirely — index
    ///     38 answers 200 with a blank white strip, so "the highest rung at or below it" would file
    ///     the site's spacer art under ABYSS ABSOLUTE.
    /// </summary>
    private static (string Gem, int FirstBadge, int TierStart, int Step)? RungFor(int badge)
    {
        foreach (var rung in DerivedLadder)
            if (badge >= rung.FirstBadge && badge <= rung.FirstBadge + (rung.Step == 0 ? 0 : 4))
                return rung;
        return null;
    }

    private static string LabelFor(int badge)
    {
        if (RungFor(badge) is not { } rung) return "(not on the ladder)";
        return rung.Step == 0 ? rung.Gem : $"{rung.Gem} LV.{badge - rung.FirstBadge + 1}";
    }

    private static string ThresholdSuffix(int badge)
    {
        if (RungFor(badge) is not { } rung || rung.TierStart == 0) return "";
        return $" ({rung.TierStart + rung.Step * (badge - rung.FirstBadge):N0}+)";
    }

    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Phoenix2PumbilityLevelBadgeReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    ///     The badge art itself. The board only shows badges somebody currently wears, but the files
    ///     sit at a predictable index, so walking the pattern downloads the whole ladder — including
    ///     the low rungs no top-1000 player is anywhere near. Hashing catches an index that is a
    ///     duplicate of its neighbour rather than a distinct rung.
    /// </summary>
    [LiveSiteFact]
    public async Task Every_badge_in_the_family_downloads_to_the_downloads_folder()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        Directory.CreateDirectory(DownloadDir);

        var sb = new StringBuilder();
        sb.AppendLine($"Badge family walk: /l_img/pumbility/pumbility_N.png for N = 0..{MaxBadgeIndex}");
        sb.AppendLine($"Saving to {DownloadDir}");
        sb.AppendLine();

        var found = new List<(int Index, int Bytes, string Hash)>();
        for (var n = 0; n <= MaxBadgeIndex; n++)
        {
            // The family is spelled two ways and each index answers to exactly one of them:
            // 1..9 are zero-padded, 10 and up are not. Asking only the bare form is what made the
            // bottom of the ladder look unpublished.
            var (response, url) = await FirstThatAnswers(client,
                [
                    $"https://piugame.com/l_img/pumbility/pumbility_{n}.png",
                    $"https://piugame.com/l_img/pumbility/pumbility_{n:00}.png"
                ], ct);
            using var _ = response;
            if (!response.IsSuccessStatusCode)
            {
                sb.AppendLine($"  {n,3}  {(int)response.StatusCode} {response.StatusCode}");
                continue;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            // A 404 that answers 200 with an HTML error body would otherwise land as a "badge".
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50)
            {
                sb.AppendLine($"  {n,3}  200 but not a PNG ({bytes.Length} bytes) — skipped");
                continue;
            }

            var hash = Convert.ToHexString(SHA256.HashData(bytes))[..12];
            // The source name plus the rung it means, so the folder reads as a ladder rather than
            // as 36 numbers — the mapping is the whole finding, and a bare index hides it.
            var name = $"pumbility_{n:00} - {LabelFor(n).Replace("[P.B] ", "")}{ThresholdSuffix(n)}.png";
            await File.WriteAllBytesAsync(Path.Combine(DownloadDir, name), bytes, ct);
            found.Add((n, bytes.Length, hash));
            sb.AppendLine($"  {n,3}  OK   {bytes.Length,7} bytes  sha={hash}  {Path.GetFileName(url),-22}  {name}");
            await Task.Delay(120, ct); // polite to the real site
        }

        sb.AppendLine();
        sb.AppendLine($"Downloaded {found.Count} badge images.");
        var dupes = found.GroupBy(f => f.Hash).Where(g => g.Count() > 1).ToList();
        sb.AppendLine(dupes.Count == 0
            ? "Every index is distinct art — no index is a repeat of another."
            : $"DUPLICATE ART across indices: {string.Join(" | ", dupes.Select(g => string.Join(",", g.Select(x => x.Index))))}");

        if (found.Count > 0)
        {
            var contiguous = found.Select(f => f.Index).ToList();
            sb.AppendLine($"Index range: {contiguous.Min()}..{contiguous.Max()}  " +
                          $"(gaps: {string.Join(",", Enumerable.Range(contiguous.Min(), contiguous.Max() - contiguous.Min() + 1).Except(contiguous))})");
        }

        Assert.NotEmpty(found);
        await Report(sb.ToString(), "pumbility-badge-files.txt", ct);
    }

    /// <summary>
    ///     The bracketing crawl. Every board row carries a PB value and a badge index, so grouping the
    ///     rows by badge turns the board into a set of observed intervals: the cutoff between badge k
    ///     and badge k+1 lies above the highest PB still wearing k and at or below the lowest PB
    ///     wearing k+1. Crawls the All / Single / Double tabs, because each tab prices a different pool
    ///     and so lands its wearers at different points on the same ladder.
    /// </summary>
    [LiveSiteFact]
    public async Task Board_badges_bracket_the_level_cutoffs()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);

        var sb = new StringBuilder();
        var allRows = new List<Row>();

        foreach (var (tab, label) in new[] { ("", "ALL"), ("s", "SINGLE"), ("d", "DOUBLE") })
        {
            var rows = new List<Row>();
            for (var page = 1; page <= MaxPages; page++)
            {
                var html = await client.GetStringAsync(
                    $"https://piugame.com/leaderboard/pumbility_ranking.php?t={tab}&page={page}", ct);
                var parsed = ParseRows(html, label).ToList();
                if (parsed.Count == 0) break;
                rows.AddRange(parsed);
                if (!HasNextPage(html)) break;
                await Task.Delay(250, ct); // polite to the real site
            }

            allRows.AddRange(rows);
            sb.AppendLine($"=== {label} tab: {rows.Count} rows " +
                          $"({rows.Count(r => r.Badge is not null)} with a badge) ===");
            if (rows.Count > 0)
                sb.AppendLine($"    PB span {rows.Min(r => r.Pumbility):N2} .. {rows.Max(r => r.Pumbility):N2}");
            sb.AppendLine();
        }

        Assert.NotEmpty(allRows);

        sb.AppendLine("TOP 15 ROWS OF THE ALL TAB (rank | PB | badge | worn title | name):");
        foreach (var r in allRows.Where(r => r.Tab == "ALL").Take(15))
            sb.AppendLine($"  #{r.Rank,-4} {r.Pumbility,10:N2}  {r.Badge?.ToString() ?? "-",-4}  " +
                          $"{Trunc(r.Title, 26),-26}  {r.Name}");
        sb.AppendLine();

        foreach (var tab in new[] { "ALL", "SINGLE", "DOUBLE" })
        {
            var rows = allRows.Where(r => r.Tab == tab && r.Badge is not null).ToList();
            sb.AppendLine($"########## {tab} TAB — BADGE BRACKETS ({rows.Count} badged rows) ##########");
            if (rows.Count == 0)
            {
                sb.AppendLine("  (no badges rendered on this tab)");
                sb.AppendLine();
                continue;
            }

            var groups = rows.GroupBy(r => r.Badge!.Value)
                .Select(g => (Badge: g.Key, N: g.Count(), Min: g.Min(x => x.Pumbility), Max: g.Max(x => x.Pumbility)))
                .OrderBy(g => g.Badge).ToList();

            sb.AppendLine("  badge |    n |     lowest wearer |    highest wearer | implied cutoff interval");
            foreach (var (i, g) in groups.Index())
            {
                var below = i > 0 ? groups[i - 1] : default;
                var interval = i > 0
                    ? $"( {below.Max:N2} , {g.Min:N2} ]  width {g.Min - below.Max:N2}"
                    : "(open below — no lower badge observed)";
                sb.AppendLine($"  {g.Badge,5} | {g.N,4} | {g.Min,17:N2} | {g.Max,17:N2} | {interval}");
            }

            sb.AppendLine();
            sb.AppendLine("  ROUND-NUMBER CANDIDATES INSIDE EACH INTERVAL (the cutoff is one of these):");
            foreach (var (i, g) in groups.Index().Skip(1))
            {
                var lo = groups[i - 1].Max;
                var hi = g.Min;
                var candidates = new[] { 1000, 500, 250, 200, 100, 50, 25 }
                    .Select(step => (step, values: RoundsIn(lo, hi, step)))
                    .Where(x => x.values.Count > 0 && x.values.Count <= 4)
                    .ToList();
                var best = candidates.FirstOrDefault(c => c.values.Count == 1);
                sb.AppendLine(
                    $"  {groups[i - 1].Badge}→{g.Badge}: ({lo:N2}, {hi:N2}]  " +
                    (best.values is { Count: 1 }
                        ? $"UNIQUE multiple of {best.step}: {best.values[0]:N0}"
                        : string.Join("  ", candidates.Take(2)
                            .Select(c => $"x{c.step}: {string.Join("/", c.values.Select(v => v.ToString("N0")))}"))));
            }

            sb.AppendLine();
        }

        // Anchor the observed ladder onto the gem names we already hold. A badge whose wearers all
        // sit inside one gem's range belongs to that gem, and its position within the gem's badges
        // is the level number the site never writes down.
        var allTab = allRows.Where(r => r.Tab == "ALL" && r.Badge is not null).ToList();
        if (allTab.Count > 0)
        {
            sb.AppendLine("########## BADGE → NAMED GEM TIER (All tab) ##########");
            foreach (var g in allTab.GroupBy(r => r.Badge!.Value).OrderBy(g => g.Key))
            {
                var min = g.Min(x => x.Pumbility);
                var max = g.Max(x => x.Pumbility);
                sb.AppendLine($"  badge {g.Key,3}  n={g.Count(),-4} {min,10:N2}..{max,-10:N2}  " +
                              $"lowest wearer sits in {GemFor(min)}, highest in {GemFor(max)}" +
                              (GemFor(min) == GemFor(max) ? "" : "   << SPANS A GEM BOUNDARY"));
            }

            sb.AppendLine();
        }

        // The ladder as a prediction, row by row. A single disagreement means the site re-cut its
        // levels (or the extrapolated half was wrong all along) — which is the whole reason to run
        // this again rather than trust the table.
        var predicted = allTab.Select(r => (Row: r, Expected: PredictBadge(r.Pumbility))).ToList();
        var wrong = predicted.Where(p => p.Expected != p.Row.Badge).ToList();
        sb.AppendLine("########## DERIVED LADDER, CHECKED ROW BY ROW ##########");
        sb.AppendLine($"  {predicted.Count} badged All-tab rows, {wrong.Count} disagreeing with the table.");
        foreach (var w in wrong.Take(20))
            sb.AppendLine($"    {w.Row.Name} {w.Row.Pumbility:N2}: site drew {w.Row.Badge}, " +
                          $"table says {w.Expected} ({LabelFor(w.Expected ?? 0)})");
        sb.AppendLine();
        sb.AppendLine("  badge | rung                       | pool at or above | observed wearers");
        foreach (var (gem, first, start, step) in DerivedLadder)
            for (var level = 0; level < (step == 0 ? 1 : 5); level++)
            {
                var badge = first + level;
                var n = allTab.Count(r => r.Badge == badge);
                sb.AppendLine($"  {badge,5} | {LabelFor(badge),-26} | {start + step * level,16:N0} | " +
                              (n == 0 ? "none on the board (extrapolated)" : $"{n}"));
            }

        Assert.True(wrong.Count == 0,
            $"{wrong.Count} of {predicted.Count} board rows drew a badge the derived ladder does not " +
            "predict — the PUMBILITY level cutoffs have moved. See the report.");

        // A per-row dump so the numbers can be re-cut without another crawl.
        var csv = new StringBuilder("tab,rank,name,worn_title,pumbility,badge\n");
        foreach (var r in allRows)
            csv.AppendLine(
                $"{r.Tab},{r.Rank},\"{r.Name.Replace("\"", "\"\"")}\",\"{r.Title.Replace("\"", "\"\"")}\"," +
                $"{r.Pumbility.ToString(CultureInfo.InvariantCulture)},{r.Badge?.ToString() ?? ""}");
        Directory.CreateDirectory(DownloadDir);
        var csvPath = Path.Combine(DownloadDir, "pumbility-board-rows.csv");
        await File.WriteAllTextAsync(csvPath, csv.ToString(), ct);
        sb.AppendLine($"(per-row data written to {csvPath})");

        await Report(sb.ToString(), "pumbility-level-brackets.txt", ct);
    }

    /// <summary>
    ///     Why indices 1..9 answer 404, which decides whether the bottom of the ladder is knowable.
    ///     "Never published" and "never served through a page yet" look identical from a bare GET, so
    ///     this asks the question four ways: with full browser headers and a Referer (a hotlink or
    ///     origin gate answers differently once it has one), against a control that is definitely
    ///     absent (if 1..9 answer byte-identically to <c>pumbility_9999.png</c> they simply are not
    ///     there), across alternate spellings of the path, and by reading what the personal PUMBILITY
    ///     page itself references — the one page a low-pool player renders, and therefore the one
    ///     place the low art would be named.
    /// </summary>
    [LiveSiteFact]
    public async Task Why_do_the_low_badge_indices_404()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        var sb = new StringBuilder();

        // ── 1. The personal page: everything it references, and the markup around the badge. ──
        var page = await client.GetStringAsync("https://piugame.com/my_page/pumbility.php", ct);
        sb.AppendLine($"=== my_page/pumbility.php ({page.Length} chars) ===");
        var refs = Regex.Matches(page, @"pumbility_[A-Za-z0-9_]+\.(?:png|jpg|webp|svg)")
            .Select(m => m.Value).Distinct().ToList();
        sb.AppendLine($"badge files referenced: {(refs.Count == 0 ? "(none)" : string.Join(", ", refs))}");
        foreach (Match m in Regex.Matches(page, @"(?s).{260}pumbility_\d+\.png.{260}"))
            sb.AppendLine("  CONTEXT: " + Regex.Replace(m.Value, @"\s+", " "));
        foreach (Match m in Regex.Matches(page, @"(?s).{160}level_wrap.{420}").Cast<Match>().Take(2))
            sb.AppendLine("  LEVEL_WRAP: " + Regex.Replace(m.Value, @"\s+", " "));
        sb.AppendLine();

        // Anything the page pulls in that might enumerate the family (a stylesheet naming each rung
        // would settle the question without guessing).
        sb.AppendLine("STYLESHEETS / SCRIPTS THE PAGE LOADS:");
        foreach (var asset in Regex.Matches(page, @"(?:href|src)=""([^""]+\.(?:css|js)[^""]*)""")
                     .Select(m => m.Groups[1].Value).Distinct().Take(25))
            sb.AppendLine("   " + asset);
        sb.AppendLine();

        // ── 2. Same URL, four header shapes. A gate that answers on Referer flips here. ──
        sb.AppendLine("=== INDEX 1..9 AND CONTROLS, PER HEADER SHAPE ===");
        var shapes = new (string Name, Action<HttpRequestMessage> Apply)[]
        {
            ("bare", _ => { }),
            ("browser+referer", r =>
            {
                r.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/139.0.0.0 Safari/537.36");
                r.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/png,*/*;q=0.8");
                r.Headers.TryAddWithoutValidation("Referer", "https://piugame.com/my_page/pumbility.php");
                r.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "image");
                r.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "no-cors");
                r.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            })
        };

        // 1..9 are the question; 10 and 36 are known-good controls; 9999 and a nonsense name are
        // what a definite miss looks like on this server.
        var probes = Enumerable.Range(1, 9).Select(n => $"pumbility_{n}.png")
            .Concat(["pumbility_10.png", "pumbility_36.png", "pumbility_9999.png", "pumbility_zzz.png"])
            .ToArray();

        foreach (var (shapeName, apply) in shapes)
        {
            sb.AppendLine($"-- header shape: {shapeName} --");
            foreach (var file in probes)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://piugame.com/l_img/pumbility/{file}");
                apply(request);
                using var response = await client.SendAsync(request, ct);
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                sb.AppendLine($"   {file,-22} {(int)response.StatusCode} {response.StatusCode,-12} " +
                              $"len={bytes.Length,-7} {Fingerprint(response)}");
                await Task.Delay(120, ct);
            }

            sb.AppendLine();
        }

        // ── 3. Alternate spellings for the very first rung. ──
        sb.AppendLine("=== ALTERNATE PATHS FOR INDEX 1 ===");
        foreach (var url in new[]
                 {
                     "https://piugame.com/l_img/pumbility/pumbility_01.png",
                     "https://piugame.com/l_img/pumbility/pumbility_1.webp",
                     "https://piugame.com/l_img/pumbility/pumbility_1.jpg",
                     "https://piugame.com/l_img/p2/pumbility/pumbility_1.png",
                     "https://piugame.com/l_img/pumbility/p2/pumbility_1.png",
                     "https://piugame.com/l_img/pumbility/pumbility_bronze_1.png",
                     "https://piugame.com/l_img/pumbility/",
                     "https://phoenix.piugame.com/l_img/pumbility/pumbility_1.png"
                 })
        {
            using var response = await client.GetAsync(url, ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            sb.AppendLine($"   {(int)response.StatusCode} len={bytes.Length,-7} {url}");
            await Task.Delay(120, ct);
        }

        await Report(sb.ToString(), "pumbility-low-badge-404s.txt", ct);
    }

    /// <summary>
    ///     The first of several spellings that answers 2xx, or the last response when none does.
    ///     Callers own the returned response.
    /// </summary>
    private static async Task<(HttpResponseMessage Response, string Url)> FirstThatAnswers(
        HttpClient client, IReadOnlyList<string> urls, CancellationToken ct)
    {
        HttpResponseMessage? last = null;
        var lastUrl = urls[^1];
        foreach (var url in urls)
        {
            var response = await client.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                last?.Dispose();
                return (response, url);
            }

            last?.Dispose();
            last = response;
            lastUrl = url;
        }

        return (last!, lastUrl);
    }

    /// <summary>Response fields that separate an origin miss from a cached/edge answer.</summary>
    private static string Fingerprint(HttpResponseMessage response)
    {
        var interesting = new[]
        {
            "Server", "X-Cache", "CF-Cache-Status", "Age", "ETag", "Last-Modified", "Content-Type",
            "X-Powered-By", "Via"
        };
        var parts = new List<string>();
        foreach (var name in interesting)
        {
            if (response.Headers.TryGetValues(name, out var v)) parts.Add($"{name}={string.Join("|", v)}");
            else if (response.Content.Headers.TryGetValues(name, out var cv))
                parts.Add($"{name}={string.Join("|", cv)}");
        }

        return string.Join("  ", parts);
    }

    /// <summary>
    ///     The two questions the board crawl leaves open. First: is the badge priced off the TOTAL
    ///     pool or off whichever pool the tab shows — the viewer's own "MY RANKING DATA" row renders
    ///     on all three tabs at three different pool values, so one fetch per tab answers it. Second:
    ///     does the badge render anywhere other than the board, which is what decides whether a
    ///     sub-17,000 player can be observed at all (the All board bottoms out around 17,000).
    /// </summary>
    [LiveSiteFact]
    public async Task Is_the_badge_priced_off_the_total_pool_and_where_else_does_it_render()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        var sb = new StringBuilder();

        sb.AppendLine("THE VIEWER'S OWN ROW, PER TAB (the tab changes the pool, not the player):");
        foreach (var (tab, label) in new[] { ("", "ALL"), ("s", "SINGLE"), ("d", "DOUBLE") })
        {
            var html = await client.GetStringAsync(
                $"https://piugame.com/leaderboard/pumbility_ranking.php?t={tab}&page=1", ct);
            var document = new HtmlDocument();
            document.LoadHtml(html);
            var mine = document.DocumentNode.SelectSingleNode(
                "//div[contains(@class,'my_pumblitiy_wrap')]//ul[contains(@class,'pumbilitySt')]/li");
            var score = mine?.SelectSingleNode(".//div[contains(@class,'score')]//i[contains(@class,'tt')]")
                ?.InnerText.Trim();
            var badge = mine?.SelectSingleNode(".//div[contains(@class,'score')]//img")
                ?.GetAttributeValue("src", "");
            var rank = mine?.SelectSingleNode(".//div[contains(@class,'num')]//i")?.InnerText.Trim();
            sb.AppendLine($"  {label,-7} rank={rank,-6} pool={score,-12} badge={badge ?? "(none)"}");
            await Task.Delay(250, ct);
        }

        sb.AppendLine();
        sb.AppendLine("DOES THE BADGE RENDER OFF THE BOARD?");
        foreach (var url in new[]
                 {
                     "https://piugame.com/my_page/pumbility.php",
                     "https://piugame.com/my_page/title.php",
                     "https://piugame.com/my_page/",
                     "https://piugame.com/leaderboard/over_ranking.php"
                 })
        {
            string html;
            try
            {
                html = await client.GetStringAsync(url, ct);
            }
            catch (Exception e)
            {
                sb.AppendLine($"  {url}  FAILED {e.GetType().Name}");
                continue;
            }

            var hits = Regex.Matches(html, @"pumbility_(\d+)\.png").Select(m => m.Groups[1].Value).Distinct()
                .ToList();
            sb.AppendLine($"  {url,-52} badges: {(hits.Count == 0 ? "(none)" : string.Join(",", hits))}");
            await Task.Delay(250, ct);
        }

        await Report(sb.ToString(), "pumbility-badge-scope.txt", ct);
    }

    private static List<int> RoundsIn(double exclusiveLow, double inclusiveHigh, int step)
    {
        var result = new List<int>();
        var first = (int)Math.Floor(exclusiveLow / step) * step + step;
        for (var v = first; v <= inclusiveHigh; v += step) result.Add(v);
        return result;
    }

    private static string GemFor(double pumbility)
    {
        var gem = GemLadder.Where(g => pumbility >= g.Threshold).OrderByDescending(g => g.Threshold).ToList();
        return gem.Count > 0 ? gem[0].Name : "(below BRONZE)";
    }

    private static IEnumerable<Row> ParseRows(string html, string tab)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        // Same exclusion the production parser uses: the viewer's own "MY RANKING DATA" block
        // reuses the ranking markup and would otherwise land in every page's results.
        var lis = document.DocumentNode.SelectNodes(
            "//ul[contains(@class,'pumbilitySt') and not(ancestor::div[contains(@class,'my_pumblitiy_wrap')])]/li");
        if (lis == null) yield break;

        foreach (var li in lis)
        {
            var scoreNode = li.SelectSingleNode(".//div[contains(@class,'score')]");
            var text = scoreNode?.SelectSingleNode(".//i[contains(@class,'tt')]")?.InnerText;
            if (text == null) continue;
            if (!double.TryParse(text.Replace(",", "").Trim(), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var pumbility)) continue;

            var src = scoreNode!.SelectSingleNode(".//img")?.GetAttributeValue("src", "") ?? "";
            var badgeMatch = Regex.Match(src, @"pumbility_(\d+)\.png");

            yield return new Row(
                tab,
                li.SelectSingleNode(".//div[contains(@class,'num')]//i")?.InnerText.Trim() ?? "?",
                HttpUtility.HtmlDecode(string.Join("",
                    li.SelectNodes(".//div[contains(@class,'profile_name')]")?.Select(n => n.InnerText.Trim()) ??
                    [])),
                HttpUtility.HtmlDecode(
                    li.SelectSingleNode(".//div[contains(@class,'profile_title')]")?.InnerText.Trim() ?? ""),
                pumbility,
                badgeMatch.Success ? int.Parse(badgeMatch.Groups[1].Value) : null);
        }
    }

    private static bool HasNextPage(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document.DocumentNode.SelectNodes("//i[contains(@class,'next')]")?.Any() == true
               || document.DocumentNode.SelectNodes("//i[contains(@class,'last')]")?.Any() == true;
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    private async Task Report(string report, string fileName, CancellationToken ct)
    {
        _output.WriteLine(report);
        Directory.CreateDirectory(DownloadDir);
        var path = Path.Combine(DownloadDir, fileName);
        await File.WriteAllTextAsync(path, report, ct);
        _output.WriteLine($"(report written to {path})");
    }

    private sealed record Row(string Tab, string Rank, string Name, string Title, double Pumbility, int? Badge);
}
