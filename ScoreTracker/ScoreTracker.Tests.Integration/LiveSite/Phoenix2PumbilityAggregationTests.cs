using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.Tests.Integration.LiveSite;

/// <summary>
///     Live canary for the Phoenix 2 PUMBILITY aggregation. Crawls the board's three tabs
///     (All / Singles / Doubles), matches players across them, and asserts the structural
///     signature that overall PUMBILITY is ONE merged top-50 across Singles+Doubles — not
///     the sum of two independent per-type pools:
///     <list type="bullet">
///         <item>every dual-type player satisfies max(S,D) &lt;= All &lt;= S+D, and</item>
///         <item>some strictly satisfy All &lt; S+D (impossible if All == S+D always).</item>
///     </list>
///     This is what caught the 2026-07-13 fix: the app had been summing the two pools. Goes
///     red if PIU ever changes back to (or the parser mis-reads) a two-pool total. Gated on
///     the same PIU creds as the other live-site tests; skipped in CI.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Phoenix2PumbilityAggregationTests : IClassFixture<PiuGameSessionFixture>
{
    private const int PagesPerTab = 6;

    // PIU renders decimals to two places and we sum int-floored contributions app-side, so
    // allow a couple of points of slack before calling an ordering violated.
    private const double Slack = 2.0;

    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Phoenix2PumbilityAggregationTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [LiveSiteFact]
    public async Task OverallPumbilityIsAMergedTop50NotTwoPoolsSummed()
    {
        var ct = CancellationToken.None;
        var (client, sid) = await _fixture.Api.GetSessionId(MixEnum.Phoenix2,
            PiuGameSessionFixture.Username!, PiuGameSessionFixture.Password!, ct);
        Assert.False(string.IsNullOrWhiteSpace(sid), "Phoenix 2 login produced no session id.");

        var all = await CrawlTab(client, null, ct);
        var singles = await CrawlTab(client, ChartType.Single, ct);
        var doubles = await CrawlTab(client, ChartType.Double, ct);
        Assert.NotEmpty(all);
        Assert.NotEmpty(singles);
        Assert.NotEmpty(doubles);

        // Players present on all three boards with a real value in BOTH per-type pools —
        // the only rows where the two models diverge (a single-type player has All == its
        // one pool under either model).
        var dual = (from a in all
                    where singles.TryGetValue(a.Key, out _) && doubles.TryGetValue(a.Key, out _)
                    let s = singles[a.Key]
                    let d = doubles[a.Key]
                    where s > 0 && d > 0
                    select (Name: a.Key, All: a.Value, S: s, D: d)).ToList();
        Assert.True(dual.Count >= 5,
            $"Too few dual-type players matched across tabs ({dual.Count}) to judge the aggregation.");

        // Invariant of a merged top-50: bounded below by the stronger pool (All >= max(S,D)),
        // above by the sum (All <= S+D). A violation is All below max or above the sum.
        var violations = dual.Where(r => r.All < Math.Max(r.S, r.D) - Slack || r.All > r.S + r.D + Slack).ToList();
        Assert.True(violations.Count == 0,
            "Found players whose All PUMBILITY is outside [max(S,D), S+D] — not a merged top-50: " +
            string.Join(", ", violations.Take(5).Select(r => $"{r.Name} All={r.All:N0} S={r.S:N0} D={r.D:N0}")));

        // The killer: a two-pool sum would force All == S+D for everyone. A merged top-50
        // drops charts once a player has >50 across both, so some sit strictly below S+D.
        var strictlyBelowSum = dual.Count(r => r.All < r.S + r.D - Slack);
        _output.WriteLine($"dual players: {dual.Count}; strictly below S+D (merged signature): {strictlyBelowSum}");
        Assert.True(strictlyBelowSum > 0,
            "Every dual-type player had All == S+D — that is the two-pool sum, not a merged top-50.");
    }

    private async Task<Dictionary<string, double>> CrawlTab(HttpClient client, ChartType? type, CancellationToken ct)
    {
        var map = new Dictionary<string, double>();
        for (var page = 1; page <= PagesPerTab; page++)
        {
            var result = await _fixture.Api.GetPumbilityRankings(MixEnum.Phoenix2, type, page, client, ct);
            foreach (var e in result.Entries)
            {
                var key = e.ProfileName.Trim();
                // Boards rank high→low, so the first sighting of a name is its best row.
                if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key)) map[key] = e.Pumbility;
            }

            if (result.IsEnd || result.Entries.Length == 0) break;
            await Task.Delay(300, ct); // polite to the real site
        }

        return map;
    }
}
