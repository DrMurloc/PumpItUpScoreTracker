using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.CompositionRoot;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ExplorationTests.Catalog;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Reconnaissance for the "my PUMBILITY doesn't match piugame" detector: can we tell that a
///     player's stored scores are incomplete WITHOUT paging their whole best-score list?
///     <para>
///         Two candidate cheap censuses live on my_page, and this probe maps both on both mixes:
///     </para>
///     <list type="number">
///         <item>
///             <c>my_best_score.php</c> renders a <c>Total.</c> count and a <c>?lv=</c> level
///             filter (All / 10–26 / 27over / 10over / coop), so ~19 requests yield a per-level
///             census. Its counts INCLUDE stage breaks on Phoenix 2 (the redesigned list shows
///             them) but not on Phoenix, which makes the comparison denominator mix-dependent.
///         </item>
///         <item>
///             <c>play_data.php</c> renders per-grade / per-plate counts with their own level
///             filter reaching down to level 1, and counts PASSES only. The owner reports the
///             grade counts are CUMULATIVE ("16 F" = 16 passes at F or better), which — if true —
///             makes the F bucket a clean per-level pass count and consecutive differences an
///             exact grade histogram.
///         </item>
///     </list>
///     <para>
///         The open questions this probe exists to answer, in priority order: does either total
///         reconcile against a chart list we can enumerate (i.e. is the DENOMINATOR alignable),
///         do UCS / half-double / CO-OP charts count, are the grade counts really cumulative and
///         really pass-only, and does Phoenix have the same play_data grammar as Phoenix 2.
///     </para>
///     <para>
///         Output-only by design — nothing here asserts a production rule. Raw pages land in
///         %TEMP%\piu-census (PIU_CENSUS_DUMP_DIR overrides) so the parser for the real detector
///         can be written against captured markup instead of live guesses.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class OfficialCensusProbeTests : IClassFixture<PiuGameSessionFixture>
{
    private static readonly TimeSpan Politeness = TimeSpan.FromMilliseconds(300);

    private static readonly string DumpDir =
        Environment.GetEnvironmentVariable("PIU_CENSUS_DUMP_DIR")
        ?? Path.Combine(Path.GetTempPath(), "piu-census");

    private static readonly Regex NumberRegex = new(@"\d[\d,]*", RegexOptions.Compiled);

    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public OfficialCensusProbeTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [LiveSiteFact]
    public async Task Phoenix2_official_census_probe()
    {
        var client = await _fixture.GetAuthenticatedPhoenix2Client(CancellationToken.None);
        await RunCensus(client, "https://piugame.com", "p2", MixEnum.Phoenix2, CancellationToken.None);
    }

    [LiveSiteFact]
    public async Task Phoenix_official_census_probe()
    {
        var client = await _fixture.GetAuthenticatedClient(CancellationToken.None);
        await RunCensus(client, "https://phoenix.piugame.com", "p1", MixEnum.Phoenix, CancellationToken.None);
    }

    /// <summary>
///     Phase 2, run once the grammar above was captured: does the level filter carry the counts
    ///     per level, and does the count tile's drill-in name the charts behind it?
    ///     <para>
    ///         The drill-in is a POST (<c>/ajax/user_play_log.php</c> with lv/type/division) but it
    ///         is a READ — it returns the modal's HTML and changes nothing on the account. Probing
    ///         it is the owner's explicit ask ("I don't know how/if it paginates").
    ///     </para>
    /// </summary>
    [LiveSiteFact]
    public async Task Phoenix2_play_data_drilldown_probe()
    {
        var client = await _fixture.GetAuthenticatedPhoenix2Client(CancellationToken.None);
        await DrillDown(client, "https://piugame.com", "p2", MixEnum.Phoenix2,
            new[] { "17", "12" },
            new[] { ("", "A", "grade"), ("17", "A", "grade"), ("", "mg", "plate") },
            CancellationToken.None);
    }

    [LiveSiteFact]
    public async Task Phoenix_play_data_drilldown_probe()
    {
        var client = await _fixture.GetAuthenticatedClient(CancellationToken.None);
        await DrillDown(client, "https://phoenix.piugame.com", "p1", MixEnum.Phoenix,
            new[] { "26", "25", "18" },
            // Phoenix's tiles carry no data-division and only plate types. The unfiltered mg
            // bucket is this account's largest (1,027) — if anything paginates, that does.
            new[] { ("26", "mg", ""), ("", "mg", "") },
            CancellationToken.None);
    }

    /// <summary>
    ///     Both mixes render a per-chart PUMBILITY breakdown at my_page/pumbility.php, in DIFFERENT
    ///     grammars — Phoenix 2 uses <c>li > div.top-wrap</c> with a <c>pumbility-point-sub</c>
    ///     decimal span, Phoenix uses the classic ranking-list markup with the contribution in
    ///     <c>div.score i.tt.en</c> and a total in <c>div.pumbility_total_wrap p.t2</c>. Both are
    ///     LIVE, unlike the ranking board's daily batch.
    /// </summary>
    [LiveSiteFact]
    public async Task Phoenix_pumbility_page_probe()
    {
        var client = await _fixture.GetAuthenticatedClient(CancellationToken.None);
        await PumbilityPage(client, "https://phoenix.piugame.com", "p1", CancellationToken.None);
    }

    [LiveSiteFact]
    public async Task Phoenix2_pumbility_page_probe()
    {
        var client = await _fixture.GetAuthenticatedPhoenix2Client(CancellationToken.None);
        await PumbilityPage(client, "https://piugame.com", "p2", CancellationToken.None);
    }

    private async Task PumbilityPage(HttpClient client, string baseUrl, string tag, CancellationToken ct)
    {
        Directory.CreateDirectory(DumpDir);
        var html = await Fetch(client, $"{baseUrl}/my_page/pumbility.php", ct);
        await Dump($"{tag}_pumbility.html", html, ct);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        _output.WriteLine($"total (P1 grammar div.pumbility_total_wrap p.t2): " +
                          (document.DocumentNode
                               .SelectSingleNode("//div[contains(@class,'pumbility_total_wrap')]//p[contains(@class,'t2')]")
                               ?.InnerText.Trim() ?? "(absent)"));
        _output.WriteLine($"classic rows (li with div.score i.tt.en): " +
                          $"{document.DocumentNode.SelectNodes("//li[.//div[contains(@class,'score')]//i[contains(@class,'tt')]]")?.Count ?? 0}");

        var rows = document.DocumentNode.SelectNodes("//li[div[contains(@class,'top-wrap')]]");
        _output.WriteLine($"{html.Length:N0} chars, {rows?.Count ?? 0} breakdown-shaped rows " +
                          "(Phoenix 2 grammar: li > div.top-wrap)");
        _output.WriteLine($"per-chart value spans (P2 grammar): " +
                          $"{Regex.Matches(html, "pumbility-point-sub").Count}");
        foreach (var li in (rows ?? new HtmlNodeCollection(null)).Take(3))
            _output.WriteLine($"  row: {Truncate(Collapse(li.InnerText), 120)}");

        var visible = Regex.Replace(html, @"<script[\s\S]*?</script>|<style[\s\S]*?</style>", " ",
            RegexOptions.IgnoreCase);
        visible = Collapse(Regex.Replace(visible, "<[^>]*>", " "));
        var marker = visible.IndexOf("PUMBILITY", StringComparison.OrdinalIgnoreCase);
        _output.WriteLine($"visible from first PUMBILITY mention: " +
                          $"{Truncate(marker < 0 ? visible : visible[marker..], 1500)}");
    }

    private async Task DrillDown(HttpClient client, string baseUrl, string tag, MixEnum mix,
        IReadOnlyList<string> levels, IReadOnlyList<(string Lv, string Type, string Division)> tiles,
        CancellationToken ct)
    {
        Directory.CreateDirectory(DumpDir);
        _output.WriteLine($"=== {mix} play_data drill-down — {baseUrl} ===");

        foreach (var level in levels)
        {
            await Task.Delay(Politeness, ct);
            var html = await Fetch(client, $"{baseUrl}/my_page/play_data.php?lv={level}", ct);
            await Dump($"{tag}_play_data_lv{level}.html", html, ct);
            var document = new HtmlDocument();
            document.LoadHtml(html);
            _output.WriteLine("");
            _output.WriteLine($"--- play_data.php?lv={level} ---");
            _output.WriteLine($"  clear: {ClearCount(document) ?? "(no clear_w block)"}");
            foreach (var (division, type, count) in CountTiles(document))
                _output.WriteLine($"  {division,-6} {type,-9} {count}");
        }

        foreach (var (lv, type, division) in tiles)
        {
            await Task.Delay(Politeness, ct);
            var form = new List<KeyValuePair<string, string>>
                { new("lv", lv), new("type", type) };
            if (division.Length > 0) form.Add(new KeyValuePair<string, string>("division", division));
            var response = await client.PostAsync($"{baseUrl}/ajax/user_play_log.php",
                new FormUrlEncodedContent(form), ct);
            var html = await response.Content.ReadAsStringAsync(ct);
            await Dump($"{tag}_play_log_lv{(lv.Length == 0 ? "all" : lv)}_{type}.html", html, ct);

            var document = new HtmlDocument();
            document.LoadHtml(html);
            var cards = document.DocumentNode.SelectNodes("//li[.//div[contains(@class,'song_name')]]")
                        ?? document.DocumentNode.SelectNodes("//ul//li");
            _output.WriteLine("");
            _output.WriteLine($"--- POST user_play_log lv='{lv}' type='{type}' division='{division}' " +
                              $"→ {(int)response.StatusCode} {html.Length:N0} chars, {cards?.Count ?? 0} li ---");
            _output.WriteLine($"  pager chrome: {(Regex.IsMatch(html, @"page|paging|next|last", RegexOptions.IgnoreCase) ? "PRESENT — inspect" : "none")}");
            foreach (var name in (document.DocumentNode.SelectNodes("//div[contains(@class,'song_name')]")
                                  ?? new HtmlNodeCollection(null)).Take(3))
                _output.WriteLine($"  first rows: {Collapse(name.InnerText)}");
            _output.WriteLine($"  head: {Truncate(Collapse(Regex.Replace(html, "<[^>]*>", " ")), 400)}");

            // The POST answers with a one-line script that GETs the real list — follow it, since
            // the list is what decides whether a detected gap can be NAMED without paging
            // my_best_score.
            var hop = Regex.Match(html, @"\$\.get\('([^']+)'");
            if (!hop.Success) continue;
            await Task.Delay(Politeness, ct);
            var detail = await Fetch(client, $"{baseUrl}{hop.Groups[1].Value}", ct);
            await Dump($"{tag}_play_log_detail_lv{(lv.Length == 0 ? "all" : lv)}_{type}.html", detail, ct);
            var detailDocument = new HtmlDocument();
            detailDocument.LoadHtml(detail);
            var rows = detailDocument.DocumentNode.SelectNodes("//li[.//div[contains(@class,'song_name')]]")
                       ?? detailDocument.DocumentNode.SelectNodes("//ul//li");
            _output.WriteLine($"  → {hop.Groups[1].Value}: {detail.Length:N0} chars, {rows?.Count ?? 0} rows");
            _output.WriteLine($"    pager: {(Regex.IsMatch(detail, @"class=\""[^\""]*pag|page=|xi-angle", RegexOptions.IgnoreCase) ? "CHROME PRESENT" : "none visible")}");
            foreach (var name in (detailDocument.DocumentNode
                                      .SelectNodes("//div[contains(@class,'song_name')]")
                                  ?? new HtmlNodeCollection(null)).Take(4))
                _output.WriteLine($"    row: {Truncate(Collapse(name.ParentNode.InnerText), 110)}");
        }
    }

    private static string? ClearCount(HtmlDocument document)
    {
        var node = document.DocumentNode
            .SelectSingleNode("//div[contains(@class,'clear_w')]//div[contains(@class,'t1')]");
        return node == null ? null : Collapse(node.InnerText);
    }

    /// <summary>
    ///     The count tiles: <c>a.play_log_btn.txt[data-type][data-division] &gt; i.t_num</c>,
    ///     reading "16 / 4,476" on Phoenix 2 and a bare count on Phoenix. Phoenix omits
    ///     data-division and offers plate tiles only.
    /// </summary>
    private static IEnumerable<(string Division, string Type, string Count)> CountTiles(HtmlDocument document)
    {
        var tiles = document.DocumentNode.SelectNodes("//a[contains(@class,'play_log_btn')][.//i[@class='t_num']]")
                    ?? new HtmlNodeCollection(null);
        foreach (var tile in tiles)
            yield return (tile.GetAttributeValue("data-division", "(none)"),
                tile.GetAttributeValue("data-type", "?"),
                Collapse(tile.SelectSingleNode(".//i[@class='t_num']").InnerText));
    }

    private static string Collapse(string value)
    {
        return Regex.Replace(HttpUtility.HtmlDecode(value), @"\s+", " ").Trim();
    }

    private async Task RunCensus(HttpClient client, string baseUrl, string tag, MixEnum mix, CancellationToken ct)
    {
        Directory.CreateDirectory(DumpDir);
        _output.WriteLine($"=== {mix} census probe — {baseUrl} — dumps in {DumpDir} ===");

        var account = await _fixture.Api.GetAccountData(mix, client, ct);
        _output.WriteLine($"account: {account.AccountName}");

        await BestScoreCensus(client, baseUrl, tag, ct);
        await PlayDataGrammar(client, baseUrl, tag, ct);
        await ReconcileAgainstOurRecords(client, mix, account.AccountName.ToString(), ct);
    }

    /// <summary>
    ///     The check the whole feature rests on: does the official per-level census agree with what
    ///     we actually store, **level by level**? A matching whole-account total proves nothing —
    ///     the owner's own Phoenix account matched exactly on 2,851 while being short one chart at
    ///     level 18 and long one below level 10.
    ///     <para>
    ///         Needs a populated database, so it is skipped unless
    ///         <c>CatalogProbe:ConnectionString</c> is configured (the same prod-synced local
    ///         Aspire container the catalog probes read).
    ///     </para>
    /// </summary>
    private async Task ReconcileAgainstOurRecords(HttpClient client, MixEnum mix, string gameTag,
        CancellationToken ct)
    {
        _output.WriteLine("");
        _output.WriteLine("--- census vs our records, per level ---");
        if (!CatalogProbeConfiguration.ConnectionConfigured)
        {
            _output.WriteLine("skipped: set CatalogProbe:ConnectionString to reconcile against a real database.");
            return;
        }

        await using var database = new ChartAttemptDbContext(
            new DbContextOptionsBuilder<ChartAttemptDbContext>()
                .UseSqlServer(CatalogProbeConfiguration.ConnectionString).Options,
            VerticalModelContributions.All());

        var userId = await database.Database
            .SqlQuery<Guid>($"SELECT TOP 1 Id AS Value FROM scores.[User] WHERE GameTag = {gameTag}")
            .FirstOrDefaultAsync(ct);
        if (userId == Guid.Empty)
        {
            _output.WriteLine($"skipped: no piuscores account carries the game tag '{gameTag}'.");
            return;
        }

        var mixId = await database.Database
            .SqlQuery<Guid>($"SELECT Id AS Value FROM scores.Mix WHERE Name = {mix.ToString()}")
            .FirstAsync(ct);

        // Same denominator the detector uses: passes only, bucketed by the MIX's level.
        var ours = await database.Database.SqlQuery<LevelCount>(
                $@"SELECT CASE WHEN c.Type = 'CoOp' THEN 'coop' ELSE CAST(cm.Level AS varchar(8)) END AS Bucket,
                          COUNT(*) AS Count
                   FROM scores.PhoenixRecord r
                   JOIN scores.Chart c ON c.Id = r.ChartId
                   JOIN scores.ChartMix cm ON cm.ChartId = c.Id AND cm.MixId = {mixId}
                   WHERE r.UserId = {userId} AND r.MixId = {mixId}
                     AND r.IsBroken = 0 AND r.Score IS NOT NULL
                   GROUP BY CASE WHEN c.Type = 'CoOp' THEN 'coop' ELSE CAST(cm.Level AS varchar(8)) END")
            .ToDictionaryAsync(r => r.Bucket, r => r.Count, StringComparer.Ordinal, ct);

        var landing = await _fixture.Api.GetPlayData(mix, client, "", ct);
        var mismatches = 0;
        foreach (var bucket in landing.Buckets.Where(b => b is not ("" or "10over")))
        {
            await Task.Delay(Politeness, ct);
            var theirs = await _fixture.Api.GetPlayData(mix, client, bucket, ct);
            // "27over" collapses every level above the numbered buckets; sum our side to match.
            var mine = bucket == "27over"
                ? ours.Where(kv => int.TryParse(kv.Key, out var l) && l >= 27).Sum(kv => kv.Value)
                : ours.GetValueOrDefault(bucket);
            if (theirs.Passes == mine) continue;

            mismatches++;
            _output.WriteLine($"  {bucket,-8} piugame {theirs.Passes,6:N0}   ours {mine,6:N0}   " +
                              $"{(theirs.Passes > mine ? "SHORT" : "OVER")} {Math.Abs(theirs.Passes - mine)}");
        }

        _output.WriteLine(mismatches == 0
            ? "  every level agrees"
            : $"  {mismatches} level(s) disagree — each is a chart the detector would name");
    }

    private sealed record LevelCount(string Bucket, int Count);

    // ---------- candidate 1: my_best_score.php Total. per ?lv= bucket ----------

    private async Task BestScoreCensus(HttpClient client, string baseUrl, string tag, CancellationToken ct)
    {
        _output.WriteLine("");
        _output.WriteLine("--- my_best_score.php census (?lv= buckets) ---");
        var landing = await Fetch(client, $"{baseUrl}/my_page/my_best_score.php", ct);
        await Dump($"{tag}_best_score_all.html", landing, ct);

        var document = new HtmlDocument();
        document.LoadHtml(landing);
        var buckets = LevelFilterOptions(document);
        if (buckets.Count == 0)
        {
            _output.WriteLine("NO ?lv= filter found on this page — the level census is unavailable on this mix.");
            return;
        }

        _output.WriteLine($"filter options: {string.Join(", ", buckets.Select(b => $"{b.Value}='{b.Label}'"))}");
        _output.WriteLine($"{"bucket",-12} {"Total.",10}");

        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        var allTotal = ParseTotal(document);
        _output.WriteLine($"{"(all)",-12} {Show(allTotal),10}");
        foreach (var (value, _) in buckets.Where(b => b.Value.Length > 0))
        {
            await Task.Delay(Politeness, ct);
            var html = await Fetch(client, $"{baseUrl}/my_page/my_best_score.php?lv={value}", ct);
            var page = new HtmlDocument();
            page.LoadHtml(html);
            var total = ParseTotal(page);
            if (total != null) totals[value] = total.Value;
            _output.WriteLine($"{value,-12} {Show(total),10}   (cards on page 1: {CountCards(page)})");
        }

        // Arithmetic tripwires. If these don't close, the buckets don't partition the account and
        // a census can't be compared against our per-level record counts without a fudge factor.
        var perLevel = totals.Where(kv => int.TryParse(kv.Key, out _)).Sum(kv => kv.Value);
        var over27 = totals.GetValueOrDefault("27over");
        var over10 = totals.TryGetValue("10over", out var o) ? (int?)o : null;
        var coop = totals.TryGetValue("coop", out var c) ? (int?)c : null;
        _output.WriteLine("");
        _output.WriteLine($"Σ(numeric levels) = {perLevel:N0}   +27over {over27:N0} = {perLevel + over27:N0}");
        if (over10 != null)
            _output.WriteLine($"10over = {over10:N0}  → {(over10 == perLevel + over27 ? "MATCHES" : "DIFFERS FROM")} " +
                              "Σ(numeric)+27over");
        if (allTotal != null)
        {
            var accounted = perLevel + over27 + (coop ?? 0);
            _output.WriteLine($"All = {allTotal:N0}, buckets+coop = {accounted:N0}, residual = {allTotal - accounted:N0} " +
                              "(expected: charts below level 10, which have no bucket — plus anything the " +
                              "filter hides)");
        }
    }

    /// <summary>
    ///     The level dropdown, read off the page rather than hardcoded — Phoenix and Phoenix 2
    ///     need not offer the same buckets, and that difference is itself a finding.
    /// </summary>
    private static IReadOnlyList<(string Value, string Label)> LevelFilterOptions(HtmlDocument document)
    {
        var selects = document.DocumentNode.SelectNodes("//select");
        var levelSelect = selects?.FirstOrDefault(s =>
            s.GetAttributeValue("onchange", "").Contains("lv=", StringComparison.OrdinalIgnoreCase));
        var options = levelSelect?.SelectNodes(".//option");
        return options?.Select(o => (o.GetAttributeValue("value", ""),
                HttpUtility.HtmlDecode(o.InnerText).Trim()))
            .ToList() ?? new List<(string, string)>();
    }

    private static int? ParseTotal(HtmlDocument document)
    {
        var wrap = document.DocumentNode.SelectSingleNode("//*[contains(@class,'total_wrap')]");
        var text = wrap?.InnerText;
        if (text == null) return null;
        var match = NumberRegex.Match(HttpUtility.HtmlDecode(text));
        return match.Success
            ? int.Parse(match.Value.Replace(",", ""), CultureInfo.InvariantCulture)
            : null;
    }

    private static int CountCards(HtmlDocument document)
    {
        var lis = document.DocumentNode.SelectNodes("//ul[contains(@class,'recently_playeList')]/li")
                  ?? document.DocumentNode.SelectNodes("//ul[contains(@class,'my_best_scoreList')]/li");
        return lis?.Count ?? 0;
    }

    // ---------- candidate 2: play_data.php grade / plate counts ----------

    private async Task PlayDataGrammar(HttpClient client, string baseUrl, string tag, CancellationToken ct)
    {
        _output.WriteLine("");
        _output.WriteLine("--- play_data.php grammar ---");
        await Task.Delay(Politeness, ct);
        string html;
        try
        {
            html = await Fetch(client, $"{baseUrl}/my_page/play_data.php", ct);
        }
        catch (Exception e)
        {
            _output.WriteLine($"play_data.php fetch FAILED ({e.Message}) — this mix may not have the page.");
            return;
        }

        await Dump($"{tag}_play_data.html", html, ct);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        // What filters does it offer? The owner reports level 1 and up plus CO-OP, unlike the
        // best-score page's floor of 10 — worth confirming, since sub-10 is the census's blind spot.
        foreach (var select in Nodes(document, "//select"))
        {
            var onChange = select.GetAttributeValue("onchange", "");
            var values = select.SelectNodes(".//option")?
                .Select(o => $"{o.GetAttributeValue("value", "")}='{HttpUtility.HtmlDecode(o.InnerText).Trim()}'");
            _output.WriteLine($"select onchange=\"{Truncate(onChange, 80)}\" → " +
                              $"{string.Join(", ", values ?? Enumerable.Empty<string>())}");
        }

        // The ajax drill-in the go-live recon catalogued but never used: if it lists the charts
        // behind a count, a detected gap could be named without touching the best-score pager.
        foreach (var attribute in new[] { "data-lv", "data-type", "data-division" })
        {
            var values = Regex.Matches(html, $@"{attribute}=""([^""]*)""")
                .Select(m => m.Groups[1].Value).Distinct().Take(40).ToList();
            if (values.Count > 0) _output.WriteLine($"{attribute}: {string.Join(", ", values)}");
        }

        foreach (var url in Regex.Matches(html, @"[\w/\.]*ajax/[\w_]+\.php").Select(m => m.Value).Distinct())
            _output.WriteLine($"ajax endpoint referenced: {url}");

        // The counts themselves. Without captured markup the reliable move is to print every
        // number on the page with the class path that reaches it, plus whatever grade/plate art
        // sits alongside — that IS the grammar, and it makes the real parser a five-minute job.
        _output.WriteLine("");
        _output.WriteLine("numeric leaves (class path → value [neighbouring art]):");
        var printed = 0;
        foreach (var node in Nodes(document, "//text()"))
        {
            var text = HttpUtility.HtmlDecode(node.InnerText).Trim();
            if (text.Length == 0 || !Regex.IsMatch(text, @"^\d[\d,]*$")) continue;
            if (InsideScriptOrStyle(node)) continue;
            if (printed++ > 200) break;

            var art = node.ParentNode?.ParentNode?.SelectNodes(".//img")
                ?.Select(i => Path.GetFileName(i.GetAttributeValue("src", "")))
                .Distinct().Take(3).ToList() ?? new List<string>();
            _output.WriteLine($"  {Truncate(ClassPath(node.ParentNode), 90),-90} {text,8}" +
                              (art.Count > 0 ? $"  [{string.Join(" ", art)}]" : ""));
        }

        _output.WriteLine("");
        _output.WriteLine("visible text (scripts/styles stripped, collapsed):");
        var visible = Regex.Replace(html, @"<script[\s\S]*?</script>|<style[\s\S]*?</style>", " ",
            RegexOptions.IgnoreCase);
        visible = Regex.Replace(HttpUtility.HtmlDecode(Regex.Replace(visible, "<[^>]*>", " ")), @"\s+", " ").Trim();
        _output.WriteLine(Truncate(visible, 4000));
    }

    private static bool InsideScriptOrStyle(HtmlNode node)
    {
        for (var current = node.ParentNode; current != null; current = current.ParentNode)
            if (current.Name is "script" or "style")
                return true;
        return false;
    }

    private static string ClassPath(HtmlNode? node)
    {
        var parts = new List<string>();
        for (var current = node; current != null && current.NodeType == HtmlNodeType.Element; current = current.ParentNode)
        {
            var css = current.GetAttributeValue("class", "");
            parts.Add(css.Length == 0 ? current.Name : $"{current.Name}.{css.Replace(' ', '.')}");
            if (parts.Count >= 4) break;
        }

        parts.Reverse();
        return string.Join(" > ", parts);
    }

    // ---------- helpers ----------

    private static IEnumerable<HtmlNode> Nodes(HtmlDocument document, string xpath)
    {
        return document.DocumentNode.SelectNodes(xpath) ?? Enumerable.Empty<HtmlNode>();
    }

    private static async Task<string> Fetch(HttpClient client, string url, CancellationToken ct)
    {
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static Task Dump(string file, string html, CancellationToken ct)
    {
        return File.WriteAllTextAsync(Path.Combine(DumpDir, file), html, ct);
    }

    private static string Show(int? value)
    {
        return value?.ToString("N0", CultureInfo.InvariantCulture) ?? "(none)";
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max] + "…";
    }
}
