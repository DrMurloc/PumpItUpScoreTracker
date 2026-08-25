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
}
