using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     Who a player is ranked against on the PUMBILITY lens — their PUMBILITY peers — keyed on
///     title and resolved per mix (docs/design/pumbility-tier-list.md §5).
///     <para>
///         Phoenix 2 has a PUMBILITY title ladder with in-title rungs — [S] ADVANCED LV.1 at
///         15,000 through LV.10 at 17,250, in 250-point steps — so a rung is the peer group.
///         Phoenix 1 has no PUMBILITY-threshold titles, so its difficulty titles stand in.
///         Imperfect and deliberately so: Phoenix 1 PUMBILITY has weeks of relevance left.
///     </para>
/// </summary>
internal static class PumbilityPeers
{
    /// <summary>Every player at once, which is what the community view reads.</summary>
    public const string Community = "*";

    /// <summary>
    ///     A PUMBILITY pool is fifty charts; anything short of that is not one yet. The one
    ///     definition the writer's membership gate and the reader's resolution both use — the
    ///     two must agree or a player reads a list nobody built for them.
    /// </summary>
    public const int PoolSize = 50;

    /// <summary>Phoenix 1: the level of the player's highest difficulty title.</summary>
    public static string ForDifficultyTitleLevel(int level)
    {
        return $"L{level}";
    }

    /// <summary>
    ///     Phoenix 2: the highest PUMBILITY rung this pool total clears on the ladder matching
    ///     the chart type. Null when it clears nothing — an unranked player has no peers.
    ///     <para>
    ///         Own-type ladder only. Reading the Combined ladder as well would make a peer group a
    ///         per-viewer union of two ladders rather than a partition, so no two players would
    ///         share one and none of it could be materialized. Deferred with the rest of the
    ///         Phoenix 2 work — the ladder has no score volume behind it yet.
    ///     </para>
    /// </summary>
    public static string? ForPhoenix2Pool(ChartType chartType, double poolTotal)
    {
        return HighestRung(chartType == ChartType.Single ? PumbilityPool.Singles : PumbilityPool.Doubles,
            poolTotal);
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
