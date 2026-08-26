namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     One chart's banked step analysis, read out of the flat metric bag once so nothing
///     downstream has to know the naming scheme. Both the folder baselines and the chip
///     engine build from this, which is what keeps "what the folder measured" and "what the
///     chart shows" from drifting apart.
/// </summary>
internal sealed record ChartBadgeProfile(
    Guid ChartId,
    IReadOnlyDictionary<string, decimal> Coverage,
    IReadOnlyDictionary<string, int> DominanceRank,
    decimal? CruxPeakiness,
    IReadOnlyList<string> CruxBadges,
    decimal? CruxDuration = null,
    IReadOnlyDictionary<string, decimal>? Geometry = null)
{
    /// <summary>
    ///     A measured stance or pad share, or null where the chart predates the geometry pass —
    ///     which is not the same as zero and must never be read as one.
    /// </summary>
    public decimal? GeometryOf(string metric)
    {
        return Geometry != null && Geometry.TryGetValue(metric, out var value) ? value : null;
    }

    /// <summary>
    ///     Whether the chart brackets enough for piucenter's bracket badges to be believed
    ///     (docs/design/chart-identity.md §3.4). A chart with no geometry banked is given the
    ///     benefit of the doubt: the veto exists to overrule a bad measurement, not to silence
    ///     every chart we have not measured yet.
    /// </summary>
    public bool BracketsAreCredible =>
        GeometryOf(PiuCenterMetrics.BracketRowShare) is not { } share ||
        share >= (decimal)ChartIdentityRules.MinimumBracketRowShare;

    /// <summary>
    ///     Badges the chart really carries: measured coverage past the badge's own bar, plus
    ///     the whole-chart qualities, which have no coverage to measure and so are admitted by
    ///     piucenter's pick alone. A dominance pick under the bar is deliberately NOT presence
    ///     (docs/design/chart-identity.md §3).
    /// </summary>
    public IEnumerable<string> PresentBadges =>
        Coverage.Where(kv => kv.Value >= ChartIdentityRules.QualifyingCoverage(kv.Key)).Select(kv => kv.Key)
            .Concat(DominanceRank.Keys.Where(ChartIdentityRules.IsWholeChartBadge))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public decimal CoverageOf(string badge)
    {
        return Coverage.TryGetValue(badge, out var value) ? value : 0m;
    }

    public bool HasQualifiedPresence(string badge)
    {
        return ChartIdentityRules.IsWholeChartBadge(badge)
            ? DominanceRank.ContainsKey(badge)
            : CoverageOf(badge) >= ChartIdentityRules.QualifyingCoverage(badge);
    }

    /// <summary>Every badge the chart mentions at all — the folder's vocabulary comes from these.</summary>
    public IEnumerable<string> MentionedBadges =>
        Coverage.Keys.Concat(DominanceRank.Keys).Distinct(StringComparer.OrdinalIgnoreCase);

    public static ChartBadgeProfile From(Guid chartId, IEnumerable<ChartSkillMetric> metrics)
    {
        var coverage = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var dominance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cruxRanks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var geometry = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        decimal? peakiness = null;
        decimal? duration = null;

        foreach (var metric in metrics)
            if (metric.MetricName.StartsWith(PiuCenterMetrics.BadgeFractionPrefix, StringComparison.Ordinal))
                coverage[metric.MetricName[PiuCenterMetrics.BadgeFractionPrefix.Length..]] = metric.Value;
            else if (metric.MetricName.StartsWith(PiuCenterMetrics.Top3Prefix, StringComparison.Ordinal))
                dominance[metric.MetricName[PiuCenterMetrics.Top3Prefix.Length..]] = (int)metric.Value;
            else if (metric.MetricName.StartsWith(PiuCenterMetrics.CruxBadgePrefix, StringComparison.Ordinal))
                cruxRanks[metric.MetricName[PiuCenterMetrics.CruxBadgePrefix.Length..]] = (int)metric.Value;
            else if (metric.MetricName == PiuCenterMetrics.CruxPeakiness) peakiness = metric.Value;
            else if (metric.MetricName == PiuCenterMetrics.CruxDuration) duration = metric.Value;
            else if (GeometryMetrics.Contains(metric.MetricName)) geometry[metric.MetricName] = metric.Value;

        return new ChartBadgeProfile(chartId, coverage, dominance, peakiness,
            cruxRanks.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray(), duration, geometry);
    }

    private static readonly IReadOnlySet<string> GeometryMetrics = new HashSet<string>(StringComparer.Ordinal)
    {
        PiuCenterMetrics.PadShareMid4, PiuCenterMetrics.PadShareMid6, PiuCenterMetrics.StanceDiagonal,
        PiuCenterMetrics.StanceSideOn, PiuCenterMetrics.StanceCrossed, PiuCenterMetrics.BracketRowShare,
        // Not geometry, but read the same way: a folder-relative number the chip engine compares
        // against a percentile rather than a fixed threshold.
        PiuCenterMetrics.TimeUnderTension
    };
}
