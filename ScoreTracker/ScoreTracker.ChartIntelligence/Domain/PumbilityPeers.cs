using ScoreTracker.Domain.Services.Contracts;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     Who a player is ranked against on the PUMBILITY lens — their PUMBILITY peers — resolved
///     per mix (docs/design/pumbility-tier-list.md §5).
///     <para>
///         <b>Phoenix 2</b> has no key of its own here: its peers are the projector's — the
///         players whose pool of the chart type sits within 500 below and 250 above the viewer's,
///         each holding a full pool of the type (docs/design/pumbility-overhaul.md D53, D55) — and
///         the lens reads them from <see cref="ScoreProjection.PeerPools" /> at request time. A
///         window around the viewer's own pool is per viewer, so nothing is stored for it; the
///         nightly job writes the community list alone on that mix.
///     </para>
///     <para>
///         <b>Phoenix 1</b> has no per-type pool worth reading, so its difficulty titles stand in,
///         one stored list per level. Imperfect and deliberately so: Phoenix 1 PUMBILITY has weeks
///         of relevance left.
///     </para>
/// </summary>
internal static class PumbilityPeers
{
    /// <summary>Every player at once, which is what the community view reads.</summary>
    public const string Community = "*";

    /// <summary>
    ///     A PUMBILITY pool is fifty charts; anything short of that is not one yet. One definition
    ///     for the writer's membership gate, the Phoenix 1 reader's resolution and the projection's
    ///     peer rule — the three must agree or a player reads a list nobody built for them.
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
}
