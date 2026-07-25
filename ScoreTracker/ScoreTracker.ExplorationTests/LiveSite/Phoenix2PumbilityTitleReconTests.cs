using System.Text;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Reveals the names behind the masked total-PUMBILITY tiers (our <c>[P.B] ??? …</c>
///     placeholders in <see cref="ScoreTracker.Domain.Models.Titles.Phoenix2.Phoenix2TitleList" />).
///     title.php masks a tier the *service account* has not earned, so higher tiers stay "????"
///     there — but the ranking board renders every top player's *worn* title verbatim, so a
///     player who earned RED BERYL and wears it spells the name out for us. Crawls the All tab
///     deep, then aggregates each worn <c>[P.B]</c> title to the PB range of its wearers: the
///     minimum PB of a tier's wearers approximates that tier's threshold from above.
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

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
