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
                var present = coverages.Count(v => v > 0);
                return new ChartFolderBaseline(mix, type, level, badge,
                    CutoffAt(coverages, ChartIdentityRules.CoreQuantile),
                    CutoffAt(coverages, ChartIdentityRules.DrenchedQuantile),
                    PresenceCutoff(coverages, present),
                    present,
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
        // The diagonal share's low tail, which is the guard on the twistless claim: side-on
        // alone calls a chart played entirely on 45-degree lines "twistless".
        yield return GeometryRow(mix, type, level, PiuCenterMetrics.StanceDiagonal, analyzed,
            ChartIdentityRules.TwistlessDiagonalQuantile, ChartIdentityRules.TwistHeavyQuantile);
        // A sustained PICK is cheap — Monolith carries one over ten seconds of tension. The
        // claim needs the folder to agree the chart is actually long.
        yield return GeometryRow(mix, type, level, PiuCenterMetrics.TimeUnderTension, analyzed,
            ChartIdentityRules.WideQuantile, ChartIdentityRules.TwistHeavyQuantile);
        yield return SpeedRow(mix, type, level, analyzed);
    }

    /// <summary>
    ///     The folder's two outer Speed-band boundaries, so the chip engine can ask whether a
    ///     chart is Very Fast without knowing anything about tier lists — Catalog cannot see
    ///     ChartIntelligence, and this is the same arithmetic the Speed list runs.
    ///     <para>
    ///         The low cutoff is the Very Slow bound and the high one the Very Fast bound, which
    ///         is why this row alone stores an absolute value in each slot rather than a
    ///         percentile. Only the outer bands are stored: the middle three are measurements,
    ///         not claims, and nothing asks about them.
    ///     </para>
    /// </summary>
    private static ChartFolderBaseline SpeedRow(MixEnum mix, ChartType type, int level,
        IReadOnlyCollection<ChartBadgeProfile> analyzed)
    {
        var speeds = analyzed.Select(p => p.GeometryOf(PiuCenterMetrics.Nps))
            .Where(v => v is > 0)
            .Select(v => v!.Value)
            .ToArray();
        if (speeds.Length < 2)
            return new ChartFolderBaseline(mix, type, level, PiuCenterMetrics.Nps, 0m, 0m, 0m,
                speeds.Length, analyzed.Count);

        var mean = speeds.Average();
        var variance = speeds.Sum(v => (v - mean) * (v - mean)) / (speeds.Length - 1);
        var deviation = (decimal)Math.Sqrt((double)variance);
        var reach = deviation * (decimal)ChartIdentityRules.SpeedIdentityZ;
        return new ChartFolderBaseline(mix, type, level, PiuCenterMetrics.Nps,
            mean - reach, mean + reach, 0m, speeds.Length, analyzed.Count);
    }

    private static ChartFolderBaseline GeometryRow(MixEnum mix, ChartType type, int level, string metric,
        IReadOnlyCollection<ChartBadgeProfile> analyzed, double lowQuantile, double highQuantile)
    {
        var measured = analyzed.Select(p => p.GeometryOf(metric))
            .Where(v => v != null)
            .Select(v => v!.Value)
            .OrderBy(v => v)
            .ToArray();
        // Geometry rows carry no presence bar: nothing asks whether a chart "has" a pad share.
        return new ChartFolderBaseline(mix, type, level, metric,
            measured.Length == 0 ? 0m : CutoffAt(measured, lowQuantile),
            measured.Length == 0 ? 0m : CutoffAt(measured, highQuantile),
            0m, measured.Length, analyzed.Count);
    }

    /// <summary>
    ///     The coverage a chart needs before this folder counts the badge as being on it. Set by
    ///     budget rather than by a fixed number: a technique gets to claim about
    ///     <see cref="ChartIdentityRules.PresenceBudget" /> of a folder's worth of charts, so the
    ///     bar rises with how common it is here and falls when it is rare.
    ///     <para>
    ///         Where a badge is rarer than its own budget the bar collapses to "carries any at
    ///         all", which is the point: the rarest techniques have folder MAXIMUMS below the old
    ///         fixed bar, so no chart in any folder could ever say it carried one.
    ///     </para>
    /// </summary>
    private static decimal PresenceCutoff(decimal[] sortedCoverages, int presentCount)
    {
        // Zero, not a big sentinel. Nothing in the folder carries the badge, so nothing can clear
        // any bar — and IsPresent already refuses a zero coverage, which is what actually keeps
        // these out. A sentinel here has to survive a decimal(9,4) column, and decimal.MaxValue
        // does not: it threw OverflowException on the first real rebuild, from the whole-chart
        // qualities, which are mentioned by their dominance pick and never carry a coverage.
        if (presentCount == 0) return 0m;
        var allowed = ChartIdentityRules.AllowedShare((double)presentCount / sortedCoverages.Length);
        var passes = Math.Clamp((int)Math.Ceiling(allowed * sortedCoverages.Length), 1, presentCount);
        // Sorted ascending, so the pass-th value from the top is the bar with an at-or-above test.
        return sortedCoverages[sortedCoverages.Length - passes];
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
