using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Cache-key ratchet (seasons slice 0, 2026-09-12): a memory-cache key that names a mix is
///     built by <c>CacheKeys</c> (SharedKernel), never spelled at the call site. The builder's two
///     entry points say what the cached object is — a community projection (<c>Mix</c>) or the
///     viewer's own numbers (<c>Viewer</c>) — and that classification is where the seasonal view's
///     segment will land, once, instead of in twenty hand-spelled strings. A key that forgets the
///     view serves one viewer's numbers to the next with no error anywhere, which is why this is a
///     ratchet and not a convention (docs/design/seasons.md §4.5, §12.1; CLAUDE.md "Cache keys").
///     The allowlist below is the day-one inventory: counts may only go DOWN. New files get no
///     allowance.
/// </summary>
public sealed class CacheKeyTests
{
    // An interpolated string that names a mix: {mix}, {Mix}, {mixId}, {request.Mix}, {mix:…} …
    private static readonly Regex MixToken =
        new(@"\{(?:request\.|query\.|command\.|context\.Message\.)?[mM]ix(?:Id|Enum)?\b[^}]*\}", RegexOptions.Compiled);

    private static readonly Regex Interpolation = new(@"\$@?""|@\$""", RegexOptions.Compiled);

    // The three ways a statement betrays itself as a cache key.
    private static readonly Regex CacheCall =
        new(@"GetOrCreate|TryGetValue|\.Set\(|\.Remove\(", RegexOptions.Compiled);

    private static readonly Regex KeyMember =
        new(@"string\s+\w*(?:Key|Cache)\w*\s*(?:\(|=|;)|\b\w*(?:cacheKey|CacheKey)\w*\s*=", RegexOptions.Compiled);

    private static readonly Regex LocalKey = new(@"\bkey\s*=", RegexOptions.Compiled);

    private static readonly Regex StatementBreak = new(@";|\r?\n\s*\r?\n", RegexOptions.Compiled);

    private static readonly string[] SkippedFolders = { "bin", "obj", "Migrations", "wwwroot", "node_modules" };

    // The builders. A vertical may keep a local one for its eviction pairs as long as it builds
    // through the shared one; these are the only files allowed to interpolate a mix into a key.
    private static readonly HashSet<string> Builders = new(StringComparer.Ordinal)
    {
        "ScoreTracker.SharedKernel/Caching/CacheKeys.cs",
        "ScoreTracker.ScoreLedger/Application/LedgerCacheKeys.cs",
        "ScoreTracker.OfficialMirror/Application/OfficialCacheKeys.cs"
    };

    // Baseline captured 2026-09-12 at the start of seasons slice 0. Shrink-only: each migration
    // commit of the slice lowers or removes its entries, and the slice ends with this empty.
    private static readonly IReadOnlyDictionary<string, int> Allowance = new Dictionary<string, int>
    {
        ["ScoreTracker.Catalog/Application/GetHoldTickProfileHandler.cs"] = 1,
        ["ScoreTracker.Catalog/Application/SearchChartsHandler.cs"] = 1,
        ["ScoreTracker.Catalog/Infrastructure/EFChartFolderBaselineRepository.cs"] = 1,
        ["ScoreTracker.Catalog/Infrastructure/EFChartRepository.cs"] = 1,
        ["ScoreTracker.ChartIntelligence/Application/BlendedTierListHandler.cs"] = 1,
        ["ScoreTracker.ChartIntelligence/Application/ChartVerdictHandler.cs"] = 1,
        ["ScoreTracker.ChartIntelligence/Application/PersonalizedBreakdownHandler.cs"] = 1,
        ["ScoreTracker.ChartIntelligence/Application/ProjectedScoresHandler.cs"] = 1,
        ["ScoreTracker.ChartIntelligence/Application/PumbilityFoldersHandler.cs"] = 1,
        ["ScoreTracker.ChartIntelligence/Application/PumbilityPoolCompositionHandler.cs"] = 1,
        ["ScoreTracker.ChartIntelligence/Infrastructure/EFTierListRepository.cs"] = 1,
        ["ScoreTracker.Communities/Infrastructure/EFCommunitiesRepository.cs"] = 1,
        // OfficialMirror burned its entries in slice 0's mirror commit: OfficialCacheKeys and the
        // board peer reader build Mix keys through CacheKeys.
        // PlayerProgress burned its eight entries in slice 0's progress commit: the stats row and
        // the projection sweep are Viewer keys; cohorts, recap, rarity, capture and titles are Mix.
        ["ScoreTracker.Rivals/Application/PeerStandingReader.cs"] = 3,
        // ScoreLedger burned its entry in slice 0's ledger commit: the record score cache is a
        // Viewer key and LedgerCacheKeys builds through CacheKeys.
        ["ScoreTracker.WeeklyChallenge/Application/WeeklyTournamentSaga.cs"] = 1,
        ["ScoreTracker.WeeklyChallenge/Infrastructure/EFDailyStepRepository.cs"] = 1,
        ["ScoreTracker.WeeklyChallenge/Infrastructure/EFWeeklyTourneyRepository.cs"] = 1,
        ["ScoreTracker/Services/ChartUrlResolver.cs"] = 1
    };

    [Fact]
    public void CacheKeysThatNameAMixAreBuiltByCacheKeys()
    {
        var root = FindSolutionRoot();
        var counts = ProductionProjects(root)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            .Where(f => (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                        && !SkippedFolders.Any(s => f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(s)))
            .Select(f => (Path: Path.GetRelativePath(root, f).Replace('\\', '/'), Count: CountHandSpelledKeys(f)))
            .Where(x => !Builders.Contains(x.Path))
            .Where(x => x.Count > 0 || Allowance.ContainsKey(x.Path))
            .ToArray();

        var violations = new List<string>();
        foreach (var (path, count) in counts.OrderBy(c => c.Path, StringComparer.Ordinal))
        {
            var allowed = Allowance.TryGetValue(path, out var a) ? a : 0;
            if (count > allowed)
                violations.Add(
                    $"{path}: {count} hand-spelled mix cache key(s), allowance {allowed} — build it with CacheKeys.Mix(...) or CacheKeys.Viewer(...) (SharedKernel.Caching); see CLAUDE.md \"Cache keys\"");
            else if (count < allowed)
                violations.Add(
                    $"{path}: down to {count} key(s) but allowance is {allowed} — ratchet it: lower this file's entry to {count} (or remove it) in CacheKeyTests");
        }

        var scanned = counts.Select(c => c.Path).ToHashSet(StringComparer.Ordinal);
        violations.AddRange(Allowance.Keys.Where(k => !scanned.Contains(k))
            .Select(k => $"{k}: no longer exists — remove its allowance entry"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static int CountHandSpelledKeys(string file)
    {
        var text = File.ReadAllText(file);
        var usesACache = text.Contains("MemoryCache", StringComparison.Ordinal);
        var hits = 0;
        foreach (var statement in StatementBreak.Split(text))
        {
            if (!Interpolation.IsMatch(statement) || !MixToken.IsMatch(statement)) continue;
            if (CacheCall.IsMatch(statement) || KeyMember.IsMatch(statement) ||
                (usesACache && LocalKey.IsMatch(statement)))
                hits++;
        }

        return hits;
    }

    // Every ScoreTracker.* project that is not a test or exploration project.
    private static IEnumerable<string> ProductionProjects(string root)
    {
        return Directory.EnumerateDirectories(root, "ScoreTracker*", SearchOption.TopDirectoryOnly)
            .Where(d => !Path.GetFileName(d).Contains("Tests", StringComparison.Ordinal));
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
