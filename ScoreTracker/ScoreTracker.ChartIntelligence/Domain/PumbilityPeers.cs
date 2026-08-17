using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Services.Contracts;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     Who a player is ranked against on the PUMBILITY lens — their PUMBILITY peers — resolved
///     per mix (docs/design/pumbility-tier-list.md §5).
///     <para>
///         <b>Phoenix 2</b>: the players within ±3 rungs of the viewer on the PUMBILITY level
///         ladder who hold a full pool of the chart type. This is THE definition — the same one
///         the PUMBILITY page's projection draws its evidence from (docs/design/pumbility-overhaul.md
///         §4.8, D22, D23) — so the two surfaces cannot disagree about who "players like you" are.
///         The key is the <i>viewer's</i> rung: everyone standing on rung <i>r</i> reads one list,
///         computed over the players in <i>r</i>±3, which is what keeps it materializable.
///     </para>
///     <para>
///         <b>Phoenix 1</b> has no PUMBILITY level ladder, so its difficulty titles stand in.
///         Imperfect and deliberately so: Phoenix 1 PUMBILITY has weeks of relevance left.
///     </para>
/// </summary>
internal static class PumbilityPeers
{
    /// <summary>Every player at once, which is what the community view reads.</summary>
    public const string Community = "*";

    /// <summary>
    ///     A PUMBILITY pool is fifty charts; anything short of that is not one yet. One definition
    ///     for the writer's membership gate, the reader's resolution and the projection's peer
    ///     rule — the three must agree or a player reads a list nobody built for them.
    /// </summary>
    public const int PoolSize = PeerGroup.PumbilityPoolSize;

    /// <summary>
    ///     A player's PUMBILITY pool of one chart type from their priced records: the fifty highest
    ///     ratings above zero, or null when they hold fewer than fifty — a short pool is not one
    ///     yet. The nightly writer builds every player's pool with this and the reader rebuilds
    ///     the viewer's own with it, so the two cannot disagree about what a pool is.
    /// </summary>
    public static IReadOnlySet<Guid>? TopPool(IEnumerable<(Guid ChartId, double Rating)> rated)
    {
        var top = rated.Where(r => r.Rating > 0)
            .OrderByDescending(r => r.Rating)
            .Take(PoolSize)
            .Select(r => r.ChartId)
            .ToHashSet();
        return top.Count >= PoolSize ? top : null;
    }

    /// <summary>Phoenix 1: the level of the player's highest difficulty title.</summary>
    public static string ForDifficultyTitleLevel(int level)
    {
        return $"L{level}";
    }

    /// <summary>
    ///     Phoenix 2: the key for a viewer standing on <paramref name="rungIndex" /> of the ladder
    ///     (badge index 0–36) — the list counted over the players in that rung's band.
    /// </summary>
    public static string ForPhoenix2Rung(int rungIndex)
    {
        return $"R{rungIndex}";
    }

    /// <summary>Phoenix 2: the key for a viewer whose total pool is <paramref name="totalPool" />.</summary>
    public static string ForPhoenix2Total(double totalPool)
    {
        return ForPhoenix2Rung(Phoenix2PumbilityLevel.From(totalPool).Index);
    }

    /// <summary>
    ///     The rungs whose players are peers of a viewer on <paramref name="rungIndex" />: three
    ///     either side, clipped to the ladder — index 0 reaches only upward, the capstone only down.
    /// </summary>
    public static (int Lowest, int Highest) Phoenix2Band(int rungIndex)
    {
        return PeerGroup.PumbilityBand(rungIndex);
    }
}
