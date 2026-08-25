using ScoreTracker.Catalog.Contracts;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Picks a chart's chips against its folder (docs/design/chart-identity.md §3). Pure: the
///     chart's banked profile plus the folder's baselines in, display-ordered chips out.
/// </summary>
internal static class ChartIdentityBuilder
{
    public static IReadOnlyList<IdentityChipRecord> Build(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        var present = profile.PresentBadges.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1 — what almost nothing else here has. Rarest first, because the rarer it is the more
        // it is the reason someone is looking at this chart.
        var unique = present
            .Where(b => folder.TryGetValue(b, out var baseline) && baseline.IsUniqueInFolder)
            .OrderBy(b => folder[b].QualifiedCount)
            .ThenBy(b => b, StringComparer.OrdinalIgnoreCase)
            .Take(ChartIdentityRules.MaxUniqueChips)
            .ToArray();

        // 2 — what the chart is made of, judged against the folder rather than in absolutes.
        // A dominance pick is a candidate even when its coverage sits under the presence bar,
        // because their pick is a real signal about emphasis; it still has to clear the
        // folder's cutoff and the floor to be shown.
        var core = present.Concat(profile.DominanceRank.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(b => !unique.Contains(b, StringComparer.OrdinalIgnoreCase))
            .Where(b => ChartIdentityRules.IsWholeChartBadge(b)
                ? present.Contains(b)
                : folder.TryGetValue(b, out var baseline) && baseline.IsCore(profile.CoverageOf(b)))
            // Their pick leads, then whichever badge stands furthest above its folder. Margin
            // over the cutoff is the comparable measure across badges — raw coverage is not,
            // since a folder full of mid-6 and a folder with one split are different scales.
            .OrderBy(b => profile.DominanceRank.ContainsKey(b) ? 0 : 1)
            .ThenByDescending(b => Margin(profile, folder, b))
            .ThenBy(b => b, StringComparer.OrdinalIgnoreCase)
            .Take(ChartIdentityRules.MaxCoreChips)
            .ToArray();

        var chips = new List<IdentityChipRecord>();
        chips.AddRange(unique.Select(b => Chip(IdentityChipKind.Unique, b, CoverageDetail(profile, b))));
        chips.AddRange(core.Select(b => Chip(IdentityChipKind.Core, b, CoverageDetail(profile, b))));

        // 3 and 4 — the shape of the chart, and what that shape is made of. The crux badges
        // only earn a chip when the spike fired AND they say something the chips above did
        // not; a spike made of the same thing the chart is made of is not news.
        if (profile.CruxPeakiness is { } peakiness && peakiness >= ChartIdentityRules.SpikePeakiness)
        {
            chips.Add(new IdentityChipRecord(IdentityChipKind.Spike, string.Empty, string.Empty, null, peakiness));
            var shown = unique.Concat(core).ToHashSet(StringComparer.OrdinalIgnoreCase);
            chips.AddRange(profile.CruxBadges
                .Where(b => !shown.Contains(b))
                .Take(ChartIdentityRules.MaxCruxChips)
                .Select(b => Chip(IdentityChipKind.Crux, b, null)));
        }

        // 5 — nothing stood out. Rather than invent a distinction, say what piucenter said.
        if (chips.Count == 0)
            chips.AddRange(profile.DominanceRank
                .OrderBy(kv => kv.Value)
                .Take(ChartIdentityRules.MaxFallbackChips)
                .Select(kv => Chip(IdentityChipKind.Fallback, kv.Key, null)));

        return chips;
    }

    /// <summary>
    ///     How far a badge's coverage stands above what the folder asks of it. Whole-chart
    ///     qualities have no coverage, so they sort last among their group rather than
    ///     borrowing a number they do not have.
    /// </summary>
    private static decimal Margin(ChartBadgeProfile profile,
        IReadOnlyDictionary<string, ChartFolderBaseline> folder, string badge)
    {
        if (ChartIdentityRules.IsWholeChartBadge(badge)) return 0m;
        return profile.CoverageOf(badge) - (folder.TryGetValue(badge, out var baseline) ? baseline.CoreCutoff : 0m);
    }

    private static decimal? CoverageDetail(ChartBadgeProfile profile, string badge)
    {
        return ChartIdentityRules.IsWholeChartBadge(badge) ? null : profile.CoverageOf(badge);
    }

    private static IdentityChipRecord Chip(IdentityChipKind kind, string badge, decimal? detail)
    {
        return new IdentityChipRecord(kind, badge, PiuCenterBadges.DisplayName(badge),
            PiuCenterBadges.CategoryFor(badge), detail);
    }
}
