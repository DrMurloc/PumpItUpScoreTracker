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
///     allowance. The first fact sees interpolated strings only — a key built by concatenation or
///     <c>string.Format</c> is outside its reach (none exist; swept 2026-09-12) — which is why the
///     builder is the primary control and this the backstop. The second fact guards the builder's
///     other door: a <c>Mix</c> key whose parts name a player is a <c>Viewer</c> key by the builder's
///     own contract, and the slice 0 bug check found two declared the other way.
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

    // A CacheKeys.Mix( call with its balanced argument list; the balancing group rides over the
    // nested calls (nameof, casts) a key's parts are made of.
    private static readonly Regex MixCall = new(
        @"CacheKeys\.Mix\((?<args>(?:[^()]|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!)))\)",
        RegexOptions.Compiled);

    // A part that names a player. A Mix key never varies by who is looking, so one of these among
    // its parts means the entry is one player's and belongs under Viewer.
    private static readonly Regex PlayerPart = new(
        @"(?<![A-Za-z0-9_])(?:user|viewer|player)Id(?![A-Za-z0-9_])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] SkippedFolders = { "bin", "obj", "Migrations", "wwwroot", "node_modules" };

    // The builders. A vertical may keep a local one for its eviction pairs as long as it builds
    // through the shared one; these are the only files allowed to interpolate a mix into a key.
    private static readonly HashSet<string> Builders = new(StringComparer.Ordinal)
    {
        "ScoreTracker.SharedKernel/Caching/CacheKeys.cs",
        "ScoreTracker.ScoreLedger/Application/LedgerCacheKeys.cs",
        "ScoreTracker.OfficialMirror/Application/OfficialCacheKeys.cs"
    };

    // Baseline captured 2026-09-12 at the start of seasons slice 0: 27 files, 32 statements.
    // The slice's migration commits burned every entry — ScoreLedger, OfficialMirror,
    // PlayerProgress, ChartIntelligence, Catalog, Rivals, then Communities, WeeklyChallenge and
    // the web's URL resolver — and the list has been empty since. Shrink-only: it never grows
    // again; a new hand-spelled key is a build failure, not a new entry.
    private static readonly IReadOnlyDictionary<string, int> Allowance = new Dictionary<string, int>();

    [Fact]
    public void CacheKeysThatNameAMixAreBuiltByCacheKeys()
    {
        var root = FindSolutionRoot();
        var counts = ProductionFiles(root)
            .Select(f => (Path: RelativePath(root, f), Count: CountHandSpelledKeys(f)))
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

    [Fact]
    public void MixKeysNeverNameAPlayer()
    {
        var root = FindSolutionRoot();
        var violations = ProductionFiles(root)
            .SelectMany(f => MixCall.Matches(File.ReadAllText(f))
                .Select(m => m.Groups["args"].Value)
                .Where(args => PlayerPart.IsMatch(args))
                .Select(args =>
                {
                    var compact = Regex.Replace(args, @"\s+", " ");
                    return $"{RelativePath(root, f)}: CacheKeys.Mix({compact}) names a player — an entry that is one player's varies by who is looking and belongs under CacheKeys.Viewer, where the view segment lands; see CLAUDE.md \"Cache keys\"";
                }))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
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

    // Every production .cs and .razor file: the ScoreTracker.* projects that are not test or
    // exploration projects, minus build output, migrations and static assets.
    private static IEnumerable<string> ProductionFiles(string root)
    {
        return Directory.EnumerateDirectories(root, "ScoreTracker*", SearchOption.TopDirectoryOnly)
            .Where(d => !Path.GetFileName(d).Contains("Tests", StringComparison.Ordinal))
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            .Where(f => (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                        && !SkippedFolders.Any(s => f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(s)));
    }

    private static string RelativePath(string root, string file)
    {
        return Path.GetRelativePath(root, file).Replace('\\', '/');
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
