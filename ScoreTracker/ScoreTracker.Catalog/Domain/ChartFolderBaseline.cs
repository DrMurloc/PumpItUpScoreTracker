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
/// <param name="PresenceCutoff">
///     The coverage a chart needs before the badge counts as being on it at all, set so that
///     about <see cref="ChartIdentityRules.AllowedShare" /> of the folder can clear it. Rare
///     techniques therefore have a low bar and pervasive ones a high one, which is the same
///     rule saying two opposite things about brackets at S14 and at D26.
/// </param>
/// <param name="PresentCount">
///     How many of the folder's analyzed charts carry the badge AT ALL — any nonzero coverage,
///     not the count clearing the bar. This is the honest prevalence: the bar is derived from
///     it, so reading rarity back off the bar's own output would be circular, and it made
///     doublesteps — on 88% of some folders — read as rare.
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
    decimal PresenceCutoff,
    int PresentCount,
    int AnalyzedCharts)
{
    /// <summary>What share of the folder carries this badge at all.</summary>
    public double Prevalence => AnalyzedCharts > 0 ? (double)PresentCount / AnalyzedCharts : 0;

    public bool IsUniqueInFolder => AnalyzedCharts > 0 &&
                                    Prevalence > 0 &&
                                    Prevalence <= ChartIdentityRules.UniquePrevalence;

    /// <summary>Whether the chart carries the badge at all, by this folder's standard for it.</summary>
    public bool IsPresent(decimal coverage)
    {
        return coverage > 0 && coverage >= PresenceCutoff;
    }

    /// <summary>
    ///     The bar to CLAIM the chart rather than merely be on it.
    ///     <para>
    ///         The margin only applies where the presence bar is a real discriminator — that is,
    ///         where the budget bound it rather than the badge's own rarity. Below
    ///         √<see cref="ChartIdentityRules.PresenceBudget" /> prevalence every chart carrying
    ///         the badge already clears the bar, so the bar sits at the folder's own values and
    ///         asking for 1.25× of it asks for more than any chart has: the same
    ///         above-the-maximum failure the drenched rule had, moved into the margin. There,
    ///         carrying the technique at all IS the claim, which is the point of a technique
    ///         almost nothing else has.
    ///     </para>
    /// </summary>
    public decimal ClaimCoverage =>
        Prevalence <= Math.Sqrt(ChartIdentityRules.PresenceBudget)
            ? PresenceCutoff
            : PresenceCutoff * ChartIdentityRules.ClaimMarginMultiple;

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
    public bool IsDrenched(decimal coverage)
    {
        return coverage >= ChartIdentityRules.CoreCoverageFloor
               && coverage >= DrenchedCutoff
               && coverage >= ClaimCoverage;
    }
}
