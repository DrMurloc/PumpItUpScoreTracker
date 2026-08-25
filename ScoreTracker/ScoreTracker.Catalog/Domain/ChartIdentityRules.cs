namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     The one place the chart-identity policy's numbers live
///     (docs/design/chart-identity.md §3). Every one of these is owner-tunable and was
///     validated by eye against real folders — change them here and every surface moves
///     together.
/// </summary>
internal static class ChartIdentityRules
{
    /// <summary>
    ///     What it takes for a badge to count as really being on a chart. Piucenter's
    ///     dominance summary is a ranking, not a measurement: it names a chart's top three
    ///     badges however little of the chart they ride. Presence is measured coverage
    ///     clearing this bar, so a chart with a #3 pick it barely carries is not that kind
    ///     of chart — the rule Achluoias D24 earned, where a bracket_drill pick over 12.5%
    ///     measured brackets had been filing a run chart under Brackets.
    /// </summary>
    private const decimal DefaultQualifyingCoverage = 0.30m;

    /// <summary>
    ///     Badges that ride nearly every chart need a higher bar, or one of them swallows a
    ///     third of a folder on coverage alone. Calibrated 2026-07-11 against the full 050726
    ///     corpus; carried over from the deleted skill mapper, which is where they were born.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, decimal> RaisedQualifyingCoverage =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["jack"] = 0.40m,
            ["jump"] = 0.50m,
            ["run"] = 0.40m,
            ["twist_90"] = 0.40m
        };

    /// <summary>
    ///     Badges that describe the whole chart rather than a stretch of it. They are never
    ///     banked with a coverage — a null there reads as "this is true of the chart", not
    ///     "zero percent" — so presence for these comes from the dominance pick alone, and
    ///     they are never asked to clear a percentile.
    /// </summary>
    private static readonly IReadOnlySet<string> WholeChartBadges =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bursty", "sustained" };

    /// <summary>Where a badge's coverage has to land in its folder to read as core.</summary>
    public const double CoreQuantile = 0.75;

    /// <summary>
    ///     The floor under the percentile. Most badges are absent from most charts, so a
    ///     folder's 75th-percentile coverage for a rare badge is zero — without this, every
    ///     chart in the folder would clear it and the chip would say nothing.
    /// </summary>
    public const decimal CoreCoverageFloor = 0.15m;

    /// <summary>How much of a folder may carry a badge before it stops being remarkable.</summary>
    public const double UniquePrevalence = 0.12;

    /// <summary>How far a crux must run over the printed level to read as a spike.</summary>
    public const decimal SpikePeakiness = 0.7m;

    public const int MaxUniqueChips = 2;
    public const int MaxCoreChips = 3;
    public const int MaxCruxChips = 2;
    public const int MaxFallbackChips = 3;

    public static decimal QualifyingCoverage(string badge)
    {
        return RaisedQualifyingCoverage.TryGetValue(badge, out var raised) ? raised : DefaultQualifyingCoverage;
    }

    public static bool IsWholeChartBadge(string badge)
    {
        return WholeChartBadges.Contains(badge);
    }
}
