using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     What one badge looks like across one folder — the context a chip needs before it can
///     say anything (docs/design/chart-identity.md §5). "bracket_run 55%" means nothing on its
///     own; against a folder where three quarters of the charts sit under 30% it means a lot.
///     <para>
///         Folder is (mix, type, level) and never just (type, level): a chart's level moves
///         between mixes, so the same chart is measured against different company depending on
///         which catalog is being read.
///     </para>
/// </summary>
/// <param name="CoreCutoff">
///     The folder's <see cref="ChartIdentityRules.CoreQuantile" /> coverage for this badge,
///     stored as the value at that rank — a chart reads as core when its coverage is strictly
///     ABOVE it, which is what puts the chart past that share of its folder.
/// </param>
/// <param name="DrenchedCutoff">
///     The folder's <see cref="ChartIdentityRules.DrenchedQuantile" /> coverage — the bar for
///     the chart being made OF this badge rather than merely having it. A percentile rather than
///     a multiple of <paramref name="CoreCutoff" />, because a multiple is not guaranteed to
///     exist: twice the 75th percentile sat above the folder's own maximum for 108 of 345
///     badge/folder pairs, so a third of the vocabulary could never be claimed at all.
/// </param>
/// <param name="QualifiedCount">
///     How many of the folder's analyzed charts really carry the badge
///     (<see cref="ChartIdentityRules.QualifyingCoverage" />). Against
///     <paramref name="AnalyzedCharts" /> this is the prevalence the ✦ rule reads.
/// </param>
/// <param name="AnalyzedCharts">
///     Charts in the folder that have banked step analysis — the honest denominator. Counting
///     the whole folder would make every badge look rare in a folder we have only half crawled.
/// </param>
internal sealed record ChartFolderBaseline(
    MixEnum Mix,
    ChartType Type,
    int Level,
    string Badge,
    decimal CoreCutoff,
    decimal DrenchedCutoff,
    int QualifiedCount,
    int AnalyzedCharts)
{
    public bool IsUniqueInFolder => AnalyzedCharts > 0 &&
                                    (double)QualifiedCount / AnalyzedCharts <= ChartIdentityRules.UniquePrevalence;

    /// <summary>
    ///     Whether a coverage stands out in this folder. Whole-chart badges never pass here —
    ///     they carry no coverage to compare, and the chip engine admits them by presence.
    /// </summary>
    public bool IsCore(decimal coverage)
    {
        return coverage >= ChartIdentityRules.CoreCoverageFloor && coverage > CoreCutoff;
    }

    /// <summary>
    ///     Whether the chart is made of this badge. Both halves are load-bearing: the percentile
    ///     says it stands out here, and the margin over the badge's own bar says it stands out
    ///     for a reason other than the badge being rare in this folder. Without the second,
    ///     That Kitty's three scattered jack segments claim a D22 whose jack percentile is low
    ///     precisely BECAUSE almost nothing there jacks.
    /// </summary>
    public bool IsDrenched(decimal coverage, string badge)
    {
        return coverage >= ChartIdentityRules.CoreCoverageFloor
               && coverage >= DrenchedCutoff
               && coverage >= ChartIdentityRules.ClaimCoverage(badge);
    }
}
