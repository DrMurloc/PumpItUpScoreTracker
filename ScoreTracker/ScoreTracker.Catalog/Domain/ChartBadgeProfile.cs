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
    ///     Whether the file's own hold list can account for the holds the note count implies.
    ///     Set by <see cref="WithNoteCount" />; true until then, because the veto exists to
    ///     refute a specific bad reading, not to silence charts nobody has measured.
    /// </summary>
    public bool HoldsAreCredible { get; init; } = true;

    /// <summary>
    ///     A measured stance or pad share, or null where the chart predates the geometry pass —
    ///     which is not the same as zero and must never be read as one.
    /// </summary>
    public decimal? GeometryOf(string metric)
    {
        return Geometry != null && Geometry.TryGetValue(metric, out var value) ? value : null;
    }

    /// <summary>
    ///     Derives the chart's hold share where the profile meets a mix's judged note count —
    ///     the one per-mix input in the engine, which is why it arrives here instead of through
    ///     <see cref="From" />: the same profile is measured against a different count in each
    ///     catalog that carries the chart. The share is every judgement that is not a banked tap
    ///     row, over the total; the file's own hold data is never read as truth, only asked
    ///     whether it could produce that many holds at all (docs/design/chart-identity.md §3.9).
    ///     <para>
    ///         Unchanged when the count or the banked step count is missing, or when the taps
    ///         alone exceed the judged total — an arithmetically impossible file says nothing
    ///         about holds rather than something extreme.
    ///     </para>
    /// </summary>
    public ChartBadgeProfile WithNoteCount(int? noteCount)
    {
        if (noteCount is not > 0) return this;
        if (GeometryOf(PiuCenterMetrics.TapRows) is not { } taps) return this;
        var derived = noteCount.Value - taps;
        if (derived < 0) return this;

        var geometry = Geometry == null
            ? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, decimal>(Geometry, StringComparer.OrdinalIgnoreCase);
        geometry[PiuCenterMetrics.HoldShare] = derived / noteCount.Value;

        var fileTicks = GeometryOf(PiuCenterMetrics.HoldTicks);
        return this with
        {
            Geometry = geometry,
            HoldsAreCredible = fileTicks == null ||
                               derived <= fileTicks.Value * ChartIdentityRules.HoldTrustMultiple
        };
    }

    /// <summary>
    ///     Whether the chart contains the pattern a footswitch is made of. Same shape as
    ///     <see cref="BracketsAreCredible" /> and for the same reason: overrule a badge built on a
    ///     guess with a measurement that is not. A chart with no banked geometry keeps the benefit
    ///     of the doubt — the veto exists to refute a bad reading, not to silence unmeasured ones.
    /// </summary>
    public bool LimbReadsAreCredible =>
        GeometryOf(PiuCenterMetrics.RepeatedPanelShare) is not { } share ||
        share >= (decimal)ChartIdentityRules.MinimumRepeatedPanelShare;

    /// <summary>
    ///     Whether the chart brackets enough for piucenter's bracket badges to be believed
    ///     (docs/design/chart-identity.md §3.4).
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
    public IEnumerable<string> PresentBadges(IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        return Coverage.Where(kv => HasQualifiedPresence(kv.Key, folder)).Select(kv => kv.Key)
            .Concat(DominanceRank.Keys.Where(ChartIdentityRules.IsWholeChartBadge))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public decimal CoverageOf(string badge)
    {
        return Coverage.TryGetValue(badge, out var value) ? value : 0m;
    }

    public bool HasQualifiedPresence(string badge, IReadOnlyDictionary<string, ChartFolderBaseline> folder)
    {
        if (ChartIdentityRules.IsWholeChartBadge(badge)) return DominanceRank.ContainsKey(badge);
        var coverage = CoverageOf(badge);
        // A folder we have never swept falls back to the old fixed bar. Only reachable before a
        // baseline exists; every real answer is the folder's own.
        return folder.TryGetValue(badge, out var baseline)
            ? baseline.IsPresent(coverage)
            : coverage >= ChartIdentityRules.FallbackQualifyingCoverage;
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
        PiuCenterMetrics.RepeatedPanelShare, PiuCenterMetrics.SustainTime,
        // Not geometry, but read the same way: scalars the chip engine compares against a
        // percentile or a share rather than a fixed threshold. This bag is the ONLY route a
        // metric has into the engine, so a claim reading one that is missing here does not
        // misfire — it never fires at all, and says nothing about why. Speed and Longest run
        // both shipped inert for exactly that reason (field test, 2026-08-26).
        PiuCenterMetrics.TimeUnderTension, PiuCenterMetrics.Nps, PiuCenterMetrics.ChartSpan,
        // The two halves of the hold derivation (§3.9): the banked step count the share is
        // computed FROM, and the file's own tick total the trust check compares AGAINST. The
        // share itself is never banked — WithNoteCount writes it under its reserved name.
        PiuCenterMetrics.TapRows, PiuCenterMetrics.HoldTicks
    };
}
