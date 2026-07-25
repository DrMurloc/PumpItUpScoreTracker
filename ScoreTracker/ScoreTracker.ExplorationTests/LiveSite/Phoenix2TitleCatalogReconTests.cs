using System.Net;
using System.Text.RegularExpressions;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Reconciles our Phoenix 2 title catalog (<see cref="Phoenix2TitleList" />) against the live
///     my_page/title.php, which renders EVERY title — earned or not — by data-name, so one
///     authenticated read is the whole catalog. Read-only (a GET of the title page).
///     <para>
///         <b>[Legacy] titles are deliberately excluded.</b> The site ports the Phoenix 1 titles
///         into Phoenix 2 prefixed "[Legacy]"; we already carry the real Phoenix 1 list, so
///         mirroring them would double every Phoenix title under a second mix (owner call).
///     </para>
///     <para>
///         Report-only (a workbench probe, not a gate): asserts only that the crawl worked, then
///         prints the diff for a human, because "missing" needs judgment. Masked tiers (all-"?"
///         names the account hasn't revealed) are our <c>[P.B] ??? …</c> placeholders under a mask;
///         the duplicate <c>LOVERS</c> data-names are our suffixed CO-OP titles. Only genuinely-new
///         names are actionable, and their requirement text comes from the page's txt_w2 block.
///         Run on demand:
///         <c>dotnet test ScoreTracker/ScoreTracker.ExplorationTests/... --filter "FullyQualifiedName~Phoenix2TitleCatalogRecon"</c>
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Phoenix2TitleCatalogReconTests : IClassFixture<PiuGameSessionFixture>
{
    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Phoenix2TitleCatalogReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [LiveSiteFact]
    public async Task Our_catalog_covers_every_live_title_except_the_legacy_ports()
    {
        var client = await _fixture.GetAuthenticatedPhoenix2Client(CancellationToken.None);
        var account = await _fixture.Api.GetAccountData(MixEnum.Phoenix2, client, CancellationToken.None);

        Assert.NotEqual("INVALID", account.AccountName.ToString());
        Assert.NotEmpty(account.TitleEntries);

        var ours = Phoenix2TitleList.BuildList().Select(t => (string)t.Name).ToHashSet();

        var siteAll = account.TitleEntries
            .Select(t => t.Name.Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .ToArray();
        var legacy = siteAll.Where(IsLegacy).Distinct().OrderBy(n => n).ToArray();
        var site = siteAll.Where(n => !IsLegacy(n)).Distinct().ToArray();

        var missingAll = site.Where(n => !ours.Contains(n)).ToArray();
        // Masks (a title the account hasn't revealed renders as all "?") aren't new titles —
        // they're our [P.B] ??? placeholders under a different disguise. Bucket them apart so the
        // actionable list is only genuinely-new names.
        var masked = missingAll.Where(IsMasked).OrderBy(n => n.Length).ToArray();
        var missing = missingAll.Where(n => !IsMasked(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var stale = ours.Where(n => !site.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        _output.WriteLine($"Ours: {ours.Count}  |  Site non-Legacy: {site.Length}  |  [Legacy] skipped: {legacy.Length}");
        _output.WriteLine("");
        // A new name is only actionable with its requirement text (the txt_w2 block) and its
        // category color (colN) — pull the raw page once so both are in hand.
        var raw = await client.GetStringAsync("https://piugame.com/my_page/title.php", CancellationToken.None);
        var details = ExtractDetails(raw);
        string Req(string n) => details.TryGetValue(n, out var v) ? v.Requirement : "";

        // Cross-version auto-grants ("...upon owning a Phoenix Version title") are the second
        // porting mechanism next to [Legacy]: not earnable in Phoenix 2, just mirrored from a
        // Phoenix 1 title we already track. Excluded for the same reason [Legacy] is (owner call).
        var crossVersion = missing.Where(n => IsCrossVersionPort(Req(n))).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var actionable = missing
            .Where(n => !IsCrossVersionPort(Req(n)) && !IsKnownDedup(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToArray();

        _output.WriteLine($"ACTIONABLE — genuinely new, earnable, unmodeled ({actionable.Length}):");
        foreach (var n in actionable)
            _output.WriteLine($"    {n}  [{(details.TryGetValue(n, out var d) ? d.Col : "?")}]  ->  {Req(n)}");
        _output.WriteLine("");
        _output.WriteLine($"Cross-version auto-grants, excluded like [Legacy] ({crossVersion.Length}):");
        foreach (var n in crossVersion)
            _output.WriteLine($"    {n}  ->  {Req(n)}");
        _output.WriteLine("");
        _output.WriteLine($"Masked tiers the account hasn't revealed ({masked.Length}) — our [P.B] ??? placeholders:");
        foreach (var n in masked) _output.WriteLine($"    {n}  (len {n.Length})");
        _output.WriteLine("");
        _output.WriteLine($"OURS not on the live page — masked/deduped/renamed, review ({stale.Length}):");
        foreach (var n in stale) _output.WriteLine($"    {n}");
        _output.WriteLine("");
        _output.WriteLine($"[Legacy] ports on the live page, excluded ({legacy.Length}):");
        foreach (var n in legacy) _output.WriteLine($"    {n}");

        // The catalog was complete at the 2026-07-21 crawl (the only unmodeled live names are the
        // two porting mechanisms, the masked [P.B] tiers, and the LOVERS dedup). So the actionable
        // bucket is the signal: it goes red the day PIU ships a genuinely new, earnable title.
        Assert.True(actionable.Length == 0,
            $"Live title.php has {actionable.Length} new earnable title(s) to model: " +
            string.Join(" · ", actionable.Select(n => $"{n} ({Req(n)})")));
    }

    // data-name -> (colN category class, requirement text) for every <li> on the title page.
    private static Dictionary<string, (string Col, string Requirement)> ExtractDetails(string html)
    {
        var result = new Dictionary<string, (string, string)>();
        var scope = Regex.Match(html, "(?s)data_titleList2.*?</ul>");
        var body = scope.Success ? scope.Value : html;
        foreach (Match li in Regex.Matches(body, "(?s)<li[^>]*\\bdata-name=\"([^\"]*)\"[^>]*>(.*?)</li>"))
        {
            var name = WebUtility.HtmlDecode(li.Groups[1].Value).Trim();
            if (result.ContainsKey(name)) continue;
            var inner = li.Groups[2].Value;
            var col = Regex.Match(inner, "class=\"t1[^\"]*\\b(col\\d+)").Groups[1].Value;
            var req = Regex.Match(inner, "(?s)t3\\b.*?<i class=\"txt\">(.*?)</i>").Groups[1].Value;
            result[name] = (col, WebUtility.HtmlDecode(req).Trim());
        }

        return result;
    }

    private static bool IsMasked(string name) => name.Length > 0 && name.All(c => c == '?');

    // The site auto-grants these for owning the matching Phoenix 1 title — a cross-version port,
    // not a Phoenix 2 achievement. Same category as [Legacy]; excluded for the same reason.
    private static bool IsCrossVersionPort(string requirement) =>
        requirement.Contains("Phoenix Version title", StringComparison.OrdinalIgnoreCase);

    // The site ships three duplicate "LOVERS" data-names (the CO-OP play-count tiers); we model
    // them suffixed as LOVERS (Silver/Gold/Platinum), so the deduped bare "LOVERS" is not new.
    private static bool IsKnownDedup(string name) =>
        string.Equals(name, "LOVERS", StringComparison.Ordinal);

    private static bool IsLegacy(string name) =>
        name.StartsWith("[Legacy]", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Legacy ", StringComparison.OrdinalIgnoreCase);
}
