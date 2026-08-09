using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     PUMBILITY precision ratchet (owner standard, 2026-08-09): <b>nothing below the
///     presentation layer rounds a PUMBILITY value. Period.</b> A pool is fifty per-chart
///     figures that each carry a real fraction, so every discarded fraction compounds — and
///     two surfaces that discard at different points disagree about the same account.
///     <para>
///         That is not hypothetical. A session card read 17,195 while the PUMBILITY page read
///         17,173 for the same pool, because one summed fifty doubles and truncated once and
///         the other truncated fifty times and then summed. Four more places had independently
///         grown the same defect — the title ladders, the gain projection, the mirror's board
///         reconstruction and the per-chart attribution — and every suite was green throughout.
///         Convention did not hold this; that is why there is a test.
///     </para>
///     <para>
///         Rounding happens in <c>ScoreTracker.Web</c> and nowhere else: totals at
///         <c>ToString("N2")</c>, gains through <c>PumbilityFormat</c>
///         (docs/UX-GUIDELINES.md). The allowlist is shrink-only — counts may go down, and a
///         new file gets no allowance.
///     </para>
/// </summary>
public sealed class PumbilityPrecisionTests
{
    // A narrowing cast applied straight to a scoring call: (int)scoring.GetScore(...),
    // (int)pumbility.GetScore(...), (int)Rating(...). This is the exact shape of every
    // instance the 17,195/17,173 investigation turned up.
    private static readonly Regex CastOnScore =
        new(@"\((?:int|long)\)\s*(?:\w+\.)*(?:GetScore|Rating)\s*\(", RegexOptions.Compiled);

    // Rounding a pool, a rating or a gain at rest. Deliberately narrow: Math.Round on a
    // percentile or a chart level is nobody's business here.
    private static readonly Regex RoundingAPool =
        new(@"Math\.(?:Round|Floor|Truncate|Ceiling)\s*\([^;]*\b(?:[Pp]umbility|[Ss]killRating|SinglesRating|DoublesRating|CoOpRating|[Pp]ool(?:Total|Value)?|[Gg]ain)\b",
            RegexOptions.Compiled);

    /// <summary>
    ///     Every project that computes PUMBILITY. Web is deliberately absent — it is the one
    ///     layer allowed to round, which is the whole rule. EventCompetition is absent for a
    ///     different reason: tournament scoring is its own scale, whole points by design, and
    ///     it shares only the <c>ScoringConfiguration</c> type with this.
    /// </summary>
    private static readonly string[] ScannedProjects =
    {
        "ScoreTracker.SharedKernel", "ScoreTracker.Domain", "ScoreTracker.Application",
        "ScoreTracker.Data", "ScoreTracker.PlayerProgress", "ScoreTracker.OfficialMirror",
        "ScoreTracker.ScoreLedger", "ScoreTracker.Communities", "ScoreTracker.ChartIntelligence",
        "ScoreTracker.Catalog"
    };

    /// <summary>
    ///     Files that match the shapes above while measuring something that is NOT PUMBILITY.
    ///     Distinct from <see cref="Allowance" /> on purpose: these are not debt and will never
    ///     shrink, so filing them as debt would make the shrink-only rule meaningless.
    /// </summary>
    private static readonly IReadOnlySet<string> Exempt = new HashSet<string>(StringComparer.Ordinal)
    {
        // A stamina-tournament session score. Whole points by design (the entity, the sum and
        // the leaderboard are all int), on a scale unrelated to a PUMBILITY pool — it only
        // matches because both go through a ScoringConfiguration named `scoring`.
        "ScoreTracker.Domain/Models/TournamentSession.cs"
    };

    // Baseline captured 2026-08-09 with the precision work landed: empty, and it should stay
    // that way. An entry here is a value being rounded before anything has decided to show it.
    private static readonly IReadOnlyDictionary<string, int> Allowance =
        new Dictionary<string, int>();

    [Fact]
    public void NothingBelowThePresentationLayerRoundsPumbility()
    {
        var root = FindSolutionRoot();
        var counts = ScannedProjects
            .Select(p => Path.Combine(root, p))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .Select(f => (Path: Path.GetRelativePath(root, f).Replace('\\', '/'), Count: CountRoundings(f)))
            .Where(x => !Exempt.Contains(x.Path))
            .Where(x => x.Count > 0 || Allowance.ContainsKey(x.Path))
            .ToArray();

        var violations = new List<string>();
        foreach (var (path, count) in counts.OrderBy(c => c.Path, StringComparer.Ordinal))
        {
            var allowed = Allowance.TryGetValue(path, out var a) ? a : 0;
            if (count > allowed)
                violations.Add(
                    $"{path}: {count} PUMBILITY rounding(s), allowance {allowed} — carry the double through and round in Web (ToString(\"N2\") for a total, PumbilityFormat.Gain for a gain)");
            else if (count < allowed)
                violations.Add(
                    $"{path}: down to {count} but allowance is {allowed} — ratchet it: lower this file's entry to {count} (or remove it) in PumbilityPrecisionTests");
        }

        var scanned = counts.Select(c => c.Path).ToHashSet(StringComparer.Ordinal);
        violations.AddRange(Allowance.Keys.Where(k => !scanned.Contains(k))
            .Select(k => $"{k}: no longer exists — remove its allowance entry"));

        // An exemption pointing at a file that moved is worse than no exemption: it silently
        // stops covering the thing it named, and nothing here would have said so.
        violations.AddRange(Exempt.Where(k => !File.Exists(Path.Combine(root, k)))
            .Select(k => $"{k}: no longer exists — repoint or remove its exemption entry"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     The scan is only worth anything if it is actually looking at the files that compute
    ///     PUMBILITY. A rename or a project move that empties it would otherwise leave a green
    ///     test guarding nothing.
    /// </summary>
    [Fact]
    public void TheScanReachesThePumbilityComputingFiles()
    {
        var root = FindSolutionRoot();
        var expected = new[]
        {
            "ScoreTracker.PlayerProgress/Application/PlayerRatingSaga.cs",
            "ScoreTracker.PlayerProgress/Application/PumbilityPageSaga.cs",
            "ScoreTracker.PlayerProgress/Application/PumbilityProjectionSaga.cs",
            "ScoreTracker.PlayerProgress/Domain/PumbilityAttribution.cs",
            "ScoreTracker.Domain/Models/Titles/Phoenix2/Phoenix2TitleList.cs",
            "ScoreTracker.OfficialMirror/Application/LeaderboardHubSaga.cs"
        };

        var missing = expected.Where(p => !File.Exists(Path.Combine(root, p))).ToArray();
        Assert.True(missing.Length == 0,
            "the precision ratchet no longer covers: " + string.Join(", ", missing)
            + " — repoint it at wherever these moved, or it is guarding an empty set");
    }

    private static int CountRoundings(string file)
    {
        var text = File.ReadAllText(file);
        return CastOnScore.Matches(text).Count + RoundingAPool.Matches(text).Count;
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScoreTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName
               ?? throw new InvalidOperationException("ScoreTracker.sln not found above test bin directory");
    }
}
