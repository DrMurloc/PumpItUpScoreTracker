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

        var rows = badges.Select(badge =>
            {
                // A chart that never mentions the badge contributes a zero: it is part of the
                // folder the badge is being judged against, not an absence to be skipped.
                var coverages = analyzed.Select(p => p.CoverageOf(badge)).OrderBy(v => v).ToArray();
                return new ChartFolderBaseline(mix, type, level, badge,
                    CutoffAt(coverages, ChartIdentityRules.CoreQuantile),
                    CutoffAt(coverages, ChartIdentityRules.DrenchedQuantile),
                    analyzed.Count(p => p.HasQualifiedPresence(badge)),
                    analyzed.Count);
            })
            .ToList();

        rows.AddRange(GeometryBaselines(mix, type, level, analyzed));
        return rows;
    }

    /// <summary>
    ///     The geometry cutoffs, stored as rows in the same table under the metric's own name
    ///     (docs/design/chart-identity.md §5). They are not badges and carry no prevalence, but
    ///     they are the same shape of fact — "where does this land in its folder" — and giving
    ///     them their own table would mean a second cache and a second rebuild path for four
    ///     numbers per folder.
    ///     <para>
    ///         Each row's <see cref="ChartFolderBaseline.CoreCutoff" /> holds the LOW cutoff and
    ///         <see cref="ChartFolderBaseline.DrenchedCutoff" /> the HIGH one, so pad-share reads
    ///         its p10 and p75 from one row and side-on reads its p90 from the high slot.
    ///     </para>
    /// </summary>
    private static IEnumerable<ChartFolderBaseline> GeometryBaselines(MixEnum mix, ChartType type, int level,
        IReadOnlyCollection<ChartBadgeProfile> analyzed)
    {
        // Only charts that actually banked the measure count. A folder half of whose charts
        // predate the geometry pass would otherwise read as full of zero-width charts.
        yield return GeometryRow(mix, type, level, PiuCenterMetrics.PadShareMid4, analyzed,
            ChartIdentityRules.WideQuantile, ChartIdentityRules.PadShareFeatureQuantile);
        yield return GeometryRow(mix, type, level, PiuCenterMetrics.PadShareMid6, analyzed,
            ChartIdentityRules.WideQuantile, ChartIdentityRules.PadShareFeatureQuantile);
        yield return GeometryRow(mix, type, level, PiuCenterMetrics.StanceSideOn, analyzed,
            ChartIdentityRules.WideQuantile, ChartIdentityRules.TwistHeavyQuantile);
        // A sustained PICK is cheap — Monolith carries one over ten seconds of tension. The
        // claim needs the folder to agree the chart is actually long.
        yield return GeometryRow(mix, type, level, PiuCenterMetrics.TimeUnderTension, analyzed,
            ChartIdentityRules.WideQuantile, ChartIdentityRules.TwistHeavyQuantile);
    }

    private static ChartFolderBaseline GeometryRow(MixEnum mix, ChartType type, int level, string metric,
        IReadOnlyCollection<ChartBadgeProfile> analyzed, double lowQuantile, double highQuantile)
    {
        var measured = analyzed.Select(p => p.GeometryOf(metric))
            .Where(v => v != null)
            .Select(v => v!.Value)
            .OrderBy(v => v)
            .ToArray();
        return new ChartFolderBaseline(mix, type, level, metric,
            measured.Length == 0 ? 0m : CutoffAt(measured, lowQuantile),
            measured.Length == 0 ? 0m : CutoffAt(measured, highQuantile),
            measured.Length, analyzed.Count);
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
