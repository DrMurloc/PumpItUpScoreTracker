using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ScoreTracker.Tests.ArchitectureTests;

/// <summary>
///     Color-token ratchet (theme overhaul, 2026-07-10): UI code never hardcodes colors.
///     Pages, components, and shared layout read theme tokens — the --mix-*/--rarity-*/
///     --diff-*/--plate-* CSS custom properties emitted per mix, via ThemeScales or var(…)
///     references — so every mix theme (XX / Phoenix / Phoenix 2) re-skins the whole site
///     from one palette record. docs/UX-GUIDELINES.md is the philosophy; this test is the
///     teeth. The allowlist below is the launch-day debt: counts may only go DOWN. New
///     files get no allowance.
/// </summary>
public sealed class UiColorTokenTests
{
    // Hex color literals (#RGB / #RRGGBB / #RRGGBBAA), excluding HTML entities (&#39;).
    private static readonly Regex HexLiteral =
        new(@"(?<![\w&])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b", RegexOptions.Compiled);

    // MudBlazor palette constants (Colors.Red.Darken1, …) — a hex literal wearing a nicer name.
    private static readonly Regex ColorsConstant =
        new(@"\bColors\.\w+\.\w+", RegexOptions.Compiled);

    private static readonly string[] ScannedFolders = { "Pages", "Components", "Shared" };

    // Baseline captured 2026-07-10 after the ramp/plate token migrations. Shrink-only:
    // when a page overhaul cleans a file, lower (or remove) its entry in the same PR.
    private static readonly IReadOnlyDictionary<string, int> Allowance = new Dictionary<string, int>
    {
        ["Pages/ChartDetails.razor"] = 2,
        ["Pages/Communities/CommunityLeaderboard.razor"] = 4,
        ["Pages/Competition/MatchTournamentQualifiers.razor"] = 1,
        ["Pages/Competition/MatchTournamentQualifiersSubmit.razor"] = 4,
        ["Pages/Competition/ScoreRankings.razor"] = 7,
        ["Pages/Competition/StaminaTournament.razor"] = 2,
        ["Pages/Dev/Populate.razor"] = 4,
        ["Pages/Experiments/ChartLetterDifficulties.razor"] = 8,
        ["Pages/Experiments/GameStats.razor"] = 12,
        ["Pages/Login.razor"] = 6,
        ["Pages/Progress/PhoenixProgress.razor"] = 2,
        // The recap deck is deliberately self-styled slide art (its design doc owns its
        // palette); it stays allowlisted rather than tokenized.
        ["Pages/Progress/PhoenixRecap.razor"] = 22,
        // ChartSkills.razor burned to zero across the tier-lists overhaul (C4 shell + C7
        // radar removal) and left the allowlist entirely; the old page at /TierLists/Old
        // (26 entries) was deleted outright in the same series (C14). ChartRandomizer's
        // two entries burned in the randomizer overhaul page rebuild.
    };

    [Fact]
    public void UiCodeReadsThemeTokensNotColorLiterals()
    {
        var webRoot = Path.Combine(FindSolutionRoot(), "ScoreTracker");
        var counts = ScannedFolders
            .Select(f => Path.Combine(webRoot, f))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => (Path: Path.GetRelativePath(webRoot, f).Replace('\\', '/'), Count: CountLiterals(f)))
            .Where(x => x.Count > 0 || Allowance.ContainsKey(x.Path))
            .ToArray();

        var violations = new List<string>();
        foreach (var (path, count) in counts.OrderBy(c => c.Path, StringComparer.Ordinal))
        {
            var allowed = Allowance.TryGetValue(path, out var a) ? a : 0;
            if (count > allowed)
                violations.Add(
                    $"{path}: {count} color literal(s), allowance {allowed} — use theme tokens (ThemeScales / var(--mix-*, --rarity-*, --diff-*, --plate-*)) instead");
            else if (count < allowed)
                violations.Add(
                    $"{path}: down to {count} literal(s) but allowance is {allowed} — ratchet it: lower this file's entry to {count} (or remove it) in UiColorTokenTests");
        }

        // Entries whose file vanished are also stale debt.
        var scanned = counts.Select(c => c.Path).ToHashSet(StringComparer.Ordinal);
        violations.AddRange(Allowance.Keys.Where(k => !scanned.Contains(k))
            .Select(k => $"{k}: no longer exists — remove its allowance entry"));

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static int CountLiterals(string file)
    {
        var text = File.ReadAllText(file);
        return HexLiteral.Matches(text).Count + ColorsConstant.Matches(text).Count;
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
