using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Turns one folder's analyzed charts into its per-badge baselines
///     (docs/design/chart-identity.md §5).
/// </summary>
internal static class FolderBaselineBuilder
{
    public static IReadOnlyList<ChartFolderBaseline> Build(MixEnum mix, ChartType type, int level,
        IReadOnlyCollection<ChartBadgeProfile> analyzed)
    {
        if (analyzed.Count == 0) return Array.Empty<ChartFolderBaseline>();

        var badges = analyzed.SelectMany(p => p.MentionedBadges)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return badges.Select(badge =>
            {
                // A chart that never mentions the badge contributes a zero: it is part of the
                // folder the badge is being judged against, not an absence to be skipped.
                var coverages = analyzed.Select(p => p.CoverageOf(badge)).OrderBy(v => v).ToArray();
                return new ChartFolderBaseline(mix, type, level, badge,
                    CutoffAt(coverages, ChartIdentityRules.CoreQuantile),
                    analyzed.Count(p => p.HasQualifiedPresence(badge)),
                    analyzed.Count);
            })
            .ToArray();
    }

    /// <summary>
    ///     The value a coverage has to beat to sit past <paramref name="quantile" /> of the
    ///     folder. Returned as the value AT that rank because the comparison is strictly
    ///     greater-than: with ties — and a folder is mostly ties at zero for any given badge —
    ///     an at-or-above test would pass every chart that shares the rank, which for a rare
    ///     badge is the entire folder.
    /// </summary>
    private static decimal CutoffAt(decimal[] sortedCoverages, double quantile)
    {
        var rank = (int)Math.Ceiling(quantile * sortedCoverages.Length);
        return sortedCoverages[Math.Clamp(rank - 1, 0, sortedCoverages.Length - 1)];
    }
}
