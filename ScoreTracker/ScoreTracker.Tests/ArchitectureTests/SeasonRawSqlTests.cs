using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Seasons slice 1a (docs/design/seasons.md D12, §6.4): the four season-discriminated tables hide
///     their season rows behind the AllTime query filter, which raw SQL never meets. So a raw statement
///     that names one of those tables says which season it means — a SELECT that forgets does not fail,
///     it double-counts a player's bests the moment slice 1b writes seasonal rows, with no error
///     anywhere. Deletes are exempt: a delete by user crosses seasons on purpose, the way the purge
///     does. Same shape as the CacheKeys ratchet — a statement-level scan of production sources — and
///     zero allowance: the two raw readers that exist were born correct.
/// </summary>
public sealed class SeasonRawSqlTests
{
    // scores.PhoenixRecord, [scores].[PhoenixRecord], [{Schema}].[PhoenixRecord] — never the C# entity
    // names, which carry no schema prefix.
    private static readonly Regex TableMention = new(
        @"(?:scores\]?\.\[?|\]\.\[)(?:PhoenixRecord|PlayerStats|PlayerFolderLevel|HardmodeChart|MostHeldChart)\b",
        RegexOptions.Compiled);

    private static readonly Regex SqlVerb = new(@"\b(?:SELECT|INSERT|UPDATE|MERGE)\b", RegexOptions.Compiled);
    private static readonly Regex Delete = new(@"\bDELETE\b", RegexOptions.Compiled);

    // A C# statement ends at a semicolon and a line break; a SQL literal's own semicolons never do.
    private static readonly Regex StatementBreak = new(@";\r?\n", RegexOptions.Compiled);

    private static readonly string[] SkippedFolders = { "bin", "obj", "Migrations", "wwwroot", "node_modules" };

    [Fact]
    public void RawSqlOnASeasonTableNamesItsSeason()
    {
        var root = FindSolutionRoot();
        var violations = ProductionFiles(root)
            .SelectMany(f => StatementBreak.Split(File.ReadAllText(f))
                .Where(s => s.Contains('"') && TableMention.IsMatch(s) && SqlVerb.IsMatch(s)
                            && !Delete.IsMatch(s) && !s.Contains("SeasonId", StringComparison.Ordinal))
                .Select(s =>
                    $"{RelativePath(root, f)}: raw SQL names a season-discriminated table without SeasonId — say `SeasonId = 0` (or the season it means); the AllTime query filter never reaches raw SQL (docs/design/seasons.md D12): {Compact(s)}"))
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    private static string Compact(string statement)
    {
        var flat = Regex.Replace(statement, @"\s+", " ").Trim();
        return flat.Length <= 160 ? flat : flat.Substring(0, 160) + "…";
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
