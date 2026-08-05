using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Supplemented-placement chokepoint ratchet (2026-08-04). A snapshot holds two readings:
///     the rows piugame published, and the rows rolled up from linked public players' own
///     ledgers. They share one table and one bool, which makes correctness a matter of every
///     reader remembering a predicate — and the failure is silent. Supplemented rows reaching
///     the record books would invent world firsts; reaching the tier-list feed would move the
///     site's most-used page; reaching the Discord digest would announce them.
///     <para>
///         So the predicate lives in exactly one place. <c>EFOfficialSnapshotRepository.Scoped</c>
///         writes it, every read on that class takes a <c>PlacementScope</c> with no default to
///         compile, and nothing else queries the table. The rename merge is the one other
///         writer — it re-points placements to a surviving player and is deliberately
///         reading-agnostic, since a merged player's history is theirs under both readings.
///     </para>
///     See docs/design/supplemented-leaderboards.md §7.
/// </summary>
public sealed class SupplementedPlacementScopeTests
{
    private const string PlacementEntity = "OfficialLeaderboardPlacementEntity";

    /// <summary>
    ///     Files allowed to name the placement entity, each with the reason. Shrink this list;
    ///     never grow it without a reason that survives the paragraph above.
    /// </summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["Infrastructure/Entities/OfficialLeaderboardPlacementEntity.cs"] = "the entity itself",
        ["Infrastructure/EFOfficialSnapshotRepository.cs"] = "the chokepoint — the only reader",
        ["Infrastructure/EFOfficialPlayerIdentityRepository.cs"] =
            "the rename merge re-points placements; reading-agnostic by design",
        ["Infrastructure/EFAccountPurgeRepository.cs"] =
            "account deletion takes the supplemented rows, which are the account's own scores",
        ["Wiring/OfficialMirrorModelContribution.cs"] = "the table mapping"
    };

    [Fact]
    public void OnlyTheSnapshotRepositoryQueriesThePlacementTable()
    {
        var violations = VerticalFiles()
            .Where(f => f.Text.Contains(PlacementEntity, StringComparison.Ordinal))
            .Where(f => !Allowed.ContainsKey(f.Path))
            .Select(f =>
                $"{f.Path}: queries {PlacementEntity} directly. Placement reads go through " +
                "IOfficialSnapshotRepository, which forces a PlacementScope — otherwise supplemented " +
                "rows leak into official reads silently (supplemented-leaderboards.md §7).")
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void EveryPlacementReadOnThePortTakesAScope()
    {
        var port = File.ReadAllText(Path.Combine(VerticalRoot(), "Domain", "IOfficialSnapshotRepository.cs"));

        // A read returning placement-shaped rows must ask which reading it wants. Write and
        // delete methods are exempt: a write states the flag on the row it writes, and the
        // supplemented delete names its target in its own name.
        var placementReads = new[]
        {
            "GetPlacements", "GetBoardPlacements", "GetPlacementDetails", "GetPlayerTimeline",
            "GetSeenPlayerIds", "GetBoardFloorHistory"
        };

        // Split on the statement terminator, not on newlines: a signature wide enough to wrap
        // would otherwise read as two half-declarations and the scope on the second line would
        // look missing from the first.
        var violations = port.Split(';')
            .Select(d => string.Join(' ', d.Split('\n').Select(l => l.Trim())).Trim())
            .Where(d => placementReads.Any(m => d.Contains(m + "(", StringComparison.Ordinal)))
            .Where(d => !d.Contains("PlacementScope", StringComparison.Ordinal))
            .Select(d => $"{d} — placement reads must take a PlacementScope, with no default")
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    ///     A default on the scope would defeat the whole design: the compiler would stop asking,
    ///     and a new read would silently inherit whichever reading the author never considered.
    /// </summary>
    [Fact]
    public void ThePlacementScopeParameterHasNoDefault()
    {
        var port = File.ReadAllText(Path.Combine(VerticalRoot(), "Domain", "IOfficialSnapshotRepository.cs"));

        Assert.DoesNotContain("PlacementScope scope =", port, StringComparison.Ordinal);
    }

    private static IReadOnlyList<(string Path, string Text)> VerticalFiles()
    {
        var root = VerticalRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(f => (Path: Path.GetRelativePath(root, f).Replace('\\', '/'), Text: File.ReadAllText(f)))
            .ToArray();
    }

    private static string VerticalRoot() =>
        Path.Combine(FindSolutionRoot(), "ScoreTracker.OfficialMirror");

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ScoreTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName
               ?? throw new InvalidOperationException("ScoreTracker.sln not found above test bin directory");
    }
}
