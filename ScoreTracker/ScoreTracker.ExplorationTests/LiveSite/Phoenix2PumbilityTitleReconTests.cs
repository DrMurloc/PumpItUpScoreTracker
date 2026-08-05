using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Reveals the names behind the masked total-PUMBILITY tiers of
///     <see cref="ScoreTracker.Domain.Models.Titles.Phoenix2.Phoenix2TitleList" />.
///     title.php masks a tier the *service account* has not earned, so higher tiers stay "????"
///     there — but the ranking board renders every top player's *worn* title verbatim, so a
///     player who earned RED BERYL and wears it spells the name out for us. Crawls the All tab
///     deep, then aggregates each worn <c>[P.B]</c> title to the PB range of its wearers: the
///     minimum PB of a tier's wearers approximates that tier's threshold from above. The pair of
///     facts here is the whole method: the ranking names a tier, title.php prices it, and the
///     mask length is what pins one onto the other. The ladder was completed 2026-08-04 when
///     FEFEMZ#1489 became the first and only wearer of the 20,000 capstone, ABYSS ABSOLUTE.
///     Read-only (GETs of a login-gated ranking). Run on demand:
///     <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/... --filter "FullyQualifiedName~Phoenix2PumbilityTitleRecon"</c>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Phoenix2PumbilityTitleReconTests : IClassFixture<PiuGameSessionFixture>
{
    // ~50 rows/page on the board; 25 pages / 1200 rows covers the owner's "top 1000" with slack.
    private const int MaxPages = 25;
    private const int MaxRows = 1200;

    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Phoenix2PumbilityTitleReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [LiveSiteFact]
    public async Task Worn_PB_titles_on_the_ranking_reveal_the_masked_tier_names()
    {
        var ct = CancellationToken.None;
        var (client, sid) = await _fixture.Api.GetSessionId(MixEnum.Phoenix2,
            PiuGameSessionFixture.Username!, PiuGameSessionFixture.Password!, ct);
        Assert.False(string.IsNullOrWhiteSpace(sid), "Phoenix 2 login produced no session id.");

        var rows = new List<(int Rank, string Name, string Title, double Pumbility)>();
        for (var page = 1; page <= MaxPages && rows.Count < MaxRows; page++)
        {
            var result = await _fixture.Api.GetPumbilityRankings(MixEnum.Phoenix2, null, page, client, ct);
            foreach (var e in result.Entries)
                rows.Add((rows.Count + 1, e.ProfileName.Trim(), e.Title.Trim(), e.Pumbility));
            if (result.IsEnd || result.Entries.Length == 0) break;
            await Task.Delay(300, ct); // polite to the real site
        }

        Assert.NotEmpty(rows);

        var sb = new StringBuilder();
        sb.AppendLine($"Crawled {rows.Count} PUMBILITY ranking rows (All tab).");
        sb.AppendLine($"PB range: #{rows[0].Rank} {rows[0].Name} {rows[0].Pumbility:N2}" +
                      $"  ..  #{rows[^1].Rank} {rows[^1].Name} {rows[^1].Pumbility:N2}");
        sb.AppendLine();

        sb.AppendLine("TOP 20 ROWS (rank | PB | worn title | name):");
        foreach (var r in rows.Take(20))
            sb.AppendLine($"  #{r.Rank,-4} {r.Pumbility,10:N2}  {Trunc(r.Title, 30),-30}  {r.Name}");
        sb.AppendLine();

        // A player wears their highest earned tier, so the minimum PB among a tier's wearers
        // sits just above that tier's threshold — the signal that names the ??? threshold.
        var pb = rows.Where(r => r.Title.StartsWith("[P.B]", StringComparison.OrdinalIgnoreCase)).ToList();
        var byTitle = pb.GroupBy(r => r.Title)
            .Select(g => (Title: g.Key, Count: g.Count(), Min: g.Min(x => x.Pumbility), Max: g.Max(x => x.Pumbility)))
            .OrderBy(g => g.Min).ToList();
        sb.AppendLine($"WORN [P.B] TIERS ({pb.Count} of {rows.Count} rows wear one), sorted by min PB:");
        foreach (var g in byTitle)
            sb.AppendLine($"  {g.Title,-30}  n={g.Count,-4}  minPB={g.Min,10:N2}  maxPB={g.Max,10:N2}");
        sb.AppendLine();

        var highest = byTitle.Count > 0 ? byTitle[^1].Title : "(none)";
        sb.AppendLine($"Highest worn [P.B] tier observed: {highest}");
        sb.AppendLine($"Rows with PB >= 18000: {rows.Count(r => r.Pumbility >= 18000)}");
        sb.AppendLine($"Rows with PB >= 19000: {rows.Count(r => r.Pumbility >= 19000)}");
        sb.AppendLine($"Rows with PB >= 20000: {rows.Count(r => r.Pumbility >= 20000)}");
        sb.AppendLine();

        sb.AppendLine("ALL DISTINCT WORN TITLES (count):");
        foreach (var g in rows.GroupBy(r => r.Title).OrderByDescending(g => g.Count()))
            sb.AppendLine($"  {g.Count(),-4}  {g.Key}");

        var report = sb.ToString();
        _output.WriteLine(report);
        var path = Path.Combine(Path.GetTempPath(), "pumbility-title-recon.txt");
        await File.WriteAllTextAsync(path, report, ct);
        _output.WriteLine($"(report written to {path})");
    }

    /// <summary>
    ///     The other half of the mapping: the ranking names a worn tier but not its threshold,
    ///     while title.php lists every masked tier next to the requirement text that IS the
    ///     threshold. Printing each mask with its length, requirement and DOM neighbours pins
    ///     a revealed name onto the exact rung — a mask is only ambiguous by length, and the
    ///     requirement disambiguates it.
    /// </summary>
    [LiveSiteFact]
    public async Task Masked_tiers_on_title_php_carry_the_requirement_that_names_their_rung()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        var raw = await client.GetStringAsync("https://piugame.com/my_page/title.php", ct);

        var entries = ExtractEntries(raw);
        Assert.NotEmpty(entries);

        var sb = new StringBuilder();
        sb.AppendLine($"title.php entries in DOM order: {entries.Count}");
        sb.AppendLine();

        sb.AppendLine("MASKED ROWS (index | mask length | requirement | category):");
        foreach (var (i, e) in entries.Index().Where(x => IsMasked(x.Item.Name)))
        {
            var prev = i > 0 ? entries[i - 1].Name : "(start)";
            var next = i < entries.Count - 1 ? entries[i + 1].Name : "(end)";
            sb.AppendLine($"  #{i,-4} len={e.Name.Length,-3} [{e.Col}]  {e.Requirement}");
            sb.AppendLine($"          prev: {prev}   next: {next}");
        }

        sb.AppendLine();
        // The mask runs one character shorter than the names we hold, so data-name and the worn
        // display string are not the same string. Dump the raw <li> for the ladder's last known
        // rung and for every gem so the relationship is readable rather than inferred.
        sb.AppendLine("RAW <li> MARKUP (SINGLE MASTER control + the gem rungs):");
        var rawLis = Regex.Matches(Regex.Match(raw, "(?s)data_titleList2.*?</ul>").Value,
            "(?s)<li[^>]*\\bdata-name=\"([^\"]*)\"[^>]*>(.*?)</li>");
        foreach (var idx in new[] { 85, 116, 117, 119, 122, 123, 124 })
        {
            if (idx >= rawLis.Count) continue;
            sb.AppendLine($"  --- #{idx} ---");
            sb.AppendLine("  " + Regex.Replace(rawLis[idx].Value, @"\s+", " "));
        }

        sb.AppendLine();
        sb.AppendLine("EVERY ROW WHOSE REQUIREMENT MENTIONS PUMBILITY (index | name | requirement):");
        foreach (var (i, e) in entries.Index()
                     .Where(x => x.Item.Requirement.Contains("PUMBILITY", StringComparison.OrdinalIgnoreCase)))
            sb.AppendLine($"  #{i,-4} {Trunc(e.Name, 24),-24}  {e.Requirement}");

        // A mask counts every character; the board's reader trims. Read the top of the ranking
        // raw and print the worn title code point by code point, so "one character longer than
        // what we store" resolves to a specific character instead of a guess.
        var board = await client.GetStringAsync(
            "https://piugame.com/leaderboard/pumbility_ranking.php?t=&page=1", ct);
        sb.AppendLine();
        sb.AppendLine("RAW profile_title MARKUP, top 3 board rows:");
        foreach (Match m in Regex.Matches(board, "(?s)profile_title[^>]*>(.*?)</div>").Cast<Match>().Take(3))
        {
            var inner = m.Groups[1].Value;
            var decoded = WebUtility.HtmlDecode(Regex.Replace(inner, "<[^>]+>", ""));
            sb.AppendLine($"  raw     : {Escape(inner)}");
            sb.AppendLine($"  decoded : len={decoded.Length} trimmed={decoded.Trim().Length}  {Escape(decoded)}");
            sb.AppendLine($"  codes   : {string.Join(" ", decoded.Trim().Select(c => $"{(int)c:X2}"))}");
        }

        var report = sb.ToString();
        _output.WriteLine(report);
        var path = Path.Combine(Path.GetTempPath(), "pumbility-title-masks.txt");
        await File.WriteAllTextAsync(path, report, ct);
        _output.WriteLine($"(report written to {path})");
    }

    private static List<(string Name, string Col, string Requirement)> ExtractEntries(string html)
    {
        var result = new List<(string, string, string)>();
        var scope = Regex.Match(html, "(?s)data_titleList2.*?</ul>");
        var body = scope.Success ? scope.Value : html;
        foreach (Match li in Regex.Matches(body, "(?s)<li[^>]*\\bdata-name=\"([^\"]*)\"[^>]*>(.*?)</li>"))
        {
            var inner = li.Groups[2].Value;
            result.Add((
                WebUtility.HtmlDecode(li.Groups[1].Value).Trim(),
                Regex.Match(inner, "class=\"t1[^\"]*\\b(col\\d+)").Groups[1].Value,
                WebUtility.HtmlDecode(Regex.Match(inner, "(?s)t3\\b.*?<i class=\"txt\">(.*?)</i>").Groups[1].Value)
                    .Trim()));
        }

        return result;
    }

    private static bool IsMasked(string name) => name.Length > 0 && name.All(c => c == '?');

    private static string Escape(string s) =>
        string.Concat(s.Select(c => c switch
        {
            '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
            ' ' => "·",
            _ when c < 32 || c > 126 => $"\\u{(int)c:X4}",
            _ => c.ToString()
        }));

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
