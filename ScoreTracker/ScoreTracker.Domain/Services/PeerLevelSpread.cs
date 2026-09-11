using ScoreTracker.Domain.Services.Contracts;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     One level of a peer group's spread (docs/design/pumbility-overhaul.md D67): how many charts of the
///     level each peer keeps in their fifty, summarised the way the chart draws it, and where the viewer's
///     own fifty sits among them.
/// </summary>
/// <param name="Level">The chart level the column is for.</param>
/// <param name="PeersByCount">
///     How many peers keep exactly that many charts of the level, keyed by the count — zero included, so the
///     peers keeping none are a count like any other.
/// </param>
/// <param name="Fewest">The fewest charts of the level any peer keeps.</param>
/// <param name="FirstQuartile">The bottom of the middle half.</param>
/// <param name="Median">The median peer's count.</param>
/// <param name="ThirdQuartile">The top of the middle half.</param>
/// <param name="Most">The most charts of the level any peer keeps.</param>
/// <param name="Keeping">How many peers keep at least one.</param>
/// <param name="Mine">How many charts of the level the viewer's own fifty holds.</param>
/// <param name="PeersBelowMine">How many peers keep fewer than the viewer.</param>
/// <param name="PeersLevelWithMine">How many peers keep exactly as many as the viewer.</param>
public sealed record LevelSpreadColumn(
    int Level,
    IReadOnlyDictionary<int, int> PeersByCount,
    int Fewest,
    double FirstQuartile,
    double Median,
    double ThirdQuartile,
    int Most,
    int Keeping,
    int Mine,
    int PeersBelowMine,
    int PeersLevelWithMine);

/// <summary>
///     Where the levels sit (docs/design/pumbility-overhaul.md D67): for one chart type, a column per level of
///     how many charts of it each peer keeps in their fifty, with the viewer's own count on it. Pure — the
///     peers, their pools and the viewer's fifty are the caller's, so the Breakdown page's chart and the probe
///     that mocked it read the same arithmetic.
/// </summary>
/// <param name="Peers">Everyone the spread counts, board players included.</param>
/// <param name="BoardPeers">How many of them the official board is the only record of.</param>
/// <param name="Columns">One per level, lowest first, with no gap between the first and the last.</param>
public sealed record PeerLevelSpread(int Peers, int BoardPeers, IReadOnlyList<LevelSpreadColumn> Columns)
{
    /// <summary>
    ///     A level earns a column when at least one peer in this many keeps a chart of it, or the viewer does.
    ///     Below that a column is a handful of outliers drawn as though they were a folder the group plays.
    /// </summary>
    public const int ColumnShare = 50;

    /// <summary>
    ///     The spread of one peer group's pools. A peer the summary holds no pool for keeps nothing at every
    ///     level rather than dropping out of the count, and a chart <paramref name="levels" /> does not know —
    ///     one outside the mix's catalog — counts nowhere.
    /// </summary>
    /// <param name="summary">The peer group and each peer's fifty.</param>
    /// <param name="levels">Each chart's level in the mix.</param>
    /// <param name="mine">The charts in the viewer's own fifty of the type.</param>
    public static PeerLevelSpread Of(PeerPoolSummary summary, IReadOnlyDictionary<Guid, int> levels,
        IEnumerable<Guid> mine)
    {
        var peers = summary.Peers.ToArray();
        var boardPeers = peers.Count(p => p.IsFromBoard);
        var perPeer = peers
            .Select(p => summary.Pools.TryGetValue(p, out var pool)
                ? CountByLevel(pool, levels)
                : new Dictionary<int, int>())
            .ToArray();
        var myCounts = CountByLevel(mine, levels);

        var keepers = new Dictionary<int, int>();
        foreach (var counts in perPeer)
        foreach (var level in counts.Keys)
            keepers[level] = keepers.GetValueOrDefault(level) + 1;

        var shown = keepers.Where(kv => kv.Value * ColumnShare >= peers.Length).Select(kv => kv.Key)
            .Concat(myCounts.Keys)
            .ToArray();
        if (shown.Length == 0)
            return new PeerLevelSpread(peers.Length, boardPeers, Array.Empty<LevelSpreadColumn>());

        var lowest = shown.Min();
        var columns = Enumerable.Range(lowest, shown.Max() - lowest + 1)
            .Select(level => Column(level, perPeer.Select(c => c.GetValueOrDefault(level)).ToArray(),
                myCounts.GetValueOrDefault(level)))
            .ToArray();
        return new PeerLevelSpread(peers.Length, boardPeers, columns);
    }

    private static Dictionary<int, int> CountByLevel(IEnumerable<Guid> charts, IReadOnlyDictionary<Guid, int> levels)
    {
        return charts.Where(levels.ContainsKey)
            .GroupBy(id => levels[id])
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static LevelSpreadColumn Column(int level, int[] counts, int mine)
    {
        Array.Sort(counts);
        var byCount = counts.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        return new LevelSpreadColumn(level, byCount,
            counts.Length == 0 ? 0 : counts[0],
            Quantile(counts, 0.25),
            Quantile(counts, 0.5),
            Quantile(counts, 0.75),
            counts.Length == 0 ? 0 : counts[^1],
            counts.Count(c => c > 0),
            mine,
            counts.Count(c => c < mine),
            counts.Count(c => c == mine));
    }

    /// <summary>
    ///     A quantile of sorted counts by linear interpolation between the closest ranks, so the median of an
    ///     even group sits halfway between its middle two.
    /// </summary>
    private static double Quantile(int[] sorted, double q)
    {
        if (sorted.Length == 0) return 0;
        var position = (sorted.Length - 1) * q;
        var below = (int)Math.Floor(position);
        var above = (int)Math.Ceiling(position);
        return sorted[below] + (sorted[above] - sorted[below]) * (position - below);
    }
}
