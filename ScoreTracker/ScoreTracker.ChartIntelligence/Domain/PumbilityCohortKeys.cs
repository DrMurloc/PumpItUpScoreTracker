using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     Who a player is ranked against on the PUMBILITY lens, keyed on title and resolved per
///     mix (docs/design/pumbility-tier-list.md §5).
///     <para>
///         Phoenix 2 has a PUMBILITY title ladder with in-title rungs — [S] ADVANCED LV.1 at
///         15,000 through LV.10 at 17,250, in 250-point steps — so a rung is the cohort.
///         Phoenix 1 has no PUMBILITY-threshold titles, so its difficulty titles stand in.
///         Imperfect and deliberately so: Phoenix 1 PUMBILITY has weeks of relevance left.
///     </para>
/// </summary>
internal static class PumbilityCohortKeys
{
    /// <summary>Every player at once, which is what the community view reads.</summary>
    public const string Community = "*";

    /// <summary>Phoenix 1: the level of the player's highest difficulty title.</summary>
    public static string ForDifficultyTitleLevel(int level)
    {
        return $"L{level}";
    }

    /// <summary>
    ///     Phoenix 2: the highest PUMBILITY rung this pool total clears, for the ladder matching
    ///     the chart type. Falls back to the Total ladder for a player who has cleared none of
    ///     their own type's rungs, which is the only reading of "Singles reads Singles and
    ///     Combined" that keeps cohorts a partition — a per-viewer union of two ladders would
    ///     give every player a different peer set, and none of it could be materialized.
    ///     Null when the total clears nothing at all: an unranked player has no cohort.
    /// </summary>
    public static string? ForPhoenix2Pool(ChartType chartType, double poolTotal, double combinedTotal)
    {
        var ownPool = chartType == ChartType.Single ? PumbilityPool.Singles : PumbilityPool.Doubles;
        return HighestRung(ownPool, poolTotal) ?? HighestRung(PumbilityPool.Total, combinedTotal);
    }

    private static string? HighestRung(PumbilityPool pool, double total)
    {
        return Phoenix2TitleList.BuildList()
            .OfType<Phoenix2PumbilityTitle>()
            .Where(t => t.Pool == pool && total >= t.CompletionRequired)
            .OrderByDescending(t => t.CompletionRequired)
            .Select(t => (string)t.Name)
            .FirstOrDefault();
    }
}
