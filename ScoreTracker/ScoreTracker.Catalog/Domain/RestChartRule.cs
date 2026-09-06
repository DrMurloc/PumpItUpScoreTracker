namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     What a chart measures against its own folder, before the rule reads it. Percentiles are the
///     share of the folder scoring strictly lower, so the easiest chart in a folder sits at 0.
/// </summary>
internal sealed record RestChartMeasures(
    double StepsPerSecond,
    int StepsPercentile,
    double HoldShare,
    int HoldPercentile,
    double HardTwistShare,
    double CruxDensity,
    int CruxPercentile,
    bool HasDrillOrAnchorRun);

/// <summary>
///     Whether a chart is a rest chart, as five tests against its own folder — mix, chart type and
///     level (docs/design/march-of-murlocs.md D29).
///     <para>
///         The owner's own examples fixed the first three: 8 6 FULL SONG D23, Altale D23, Scorpion
///         King D23, Ugly Dee D18, Hi Bi D21 and Iolite Sky D20 share few steps, heavy holds and no
///         drills or anchor runs. Neither NPS nor twist share is the tell — hold ticks inflate NPS,
///         so Altale sits at its folder's median, and Scorpion King is twisty.
///     </para>
///     <para>
///         His two rejections fixed the last two. V3 D24 is hold-heavy and step-light and would pass
///         on the first three, but its hard twists cover 1.43 of the chart against at most 0.50 for
///         everything he accepted ("those twists are INTENSE"). 4NT D24's crux sits at the 73rd
///         percentile of its folder where every accepted chart is at or below the 59th.
///     </para>
/// </summary>
internal static class RestChartRule
{
    /// <summary>Steps per second: in the bottom quarter of the folder.</summary>
    public const int MaxStepsPercentile = 25;

    /// <summary>Hold share: in the top quarter of the folder.</summary>
    public const int MinHoldPercentile = 75;

    /// <summary>Hard twists (over-90 plus far) covering at most half the chart.</summary>
    public const double MaxHardTwistShare = 0.50;

    /// <summary>Crux density no higher than the folder's 60th percentile.</summary>
    public const int MaxCruxPercentile = 60;

    public static bool IsRest(RestChartMeasures m) =>
        m.StepsPercentile <= MaxStepsPercentile &&
        m.HoldPercentile >= MinHoldPercentile &&
        !m.HasDrillOrAnchorRun &&
        m.HardTwistShare <= MaxHardTwistShare &&
        m.CruxPercentile <= MaxCruxPercentile;

    /// <summary>
    ///     Where a value sits in its folder, 0 to 100: the share of the folder strictly below it. A
    ///     folder of one is 0 by construction, which fails the hold test and so never claims a rest
    ///     chart from a folder too small to have a distribution.
    /// </summary>
    public static int Percentile(IReadOnlyCollection<double> folder, double value) =>
        folder.Count == 0 ? 0 : (int)Math.Round(100.0 * folder.Count(v => v < value) / folder.Count);
}
