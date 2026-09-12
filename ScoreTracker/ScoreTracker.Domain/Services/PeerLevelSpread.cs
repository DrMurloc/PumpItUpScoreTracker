using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     One level and chart type of a cohort's fifties, before a viewer is placed on it
///     (docs/design/pumbility-overhaul.md D68). Everything here is a statement about the cohort
///     alone, which is what lets a band be read once and shared by everyone standing on it.
/// </summary>
/// <param name="Level">The chart level the band is for.</param>
/// <param name="Type">The chart type, never summed with the other one.</param>
/// <param name="HoldersByCount">
///     How many of them hold exactly that many charts of the level and type, keyed by the count —
///     zero included, so the holders holding none are a count like any other.
/// </param>
/// <param name="Fewest">The fewest any of them holds.</param>
/// <param name="FirstQuartile">The bottom of the middle half.</param>
/// <param name="Median">The median holder's count.</param>
/// <param name="ThirdQuartile">The top of the middle half.</param>
/// <param name="Most">The most any of them holds.</param>
/// <param name="Holding">How many of them hold at least one.</param>
public sealed record LevelSpreadBand(
    int Level,
    ChartType Type,
    IReadOnlyDictionary<int, int> HoldersByCount,
    int Fewest,
    double FirstQuartile,
    double Median,
    double ThirdQuartile,
    int Most,
    int Holding);

/// <summary>
///     A cohort's spread over every level any of them holds — the players standing on one band of a
///     ladder, and what their fifties are made of (docs/design/pumbility-overhaul.md D68).
///     <para>
///         Deliberately viewer-free, and the reason a cohort is read once per band rather than once
///         per viewer: every DIAMOND player is looking at the same population. The viewer is placed
///         on it by <see cref="PeerLevelSpread" />, which is also where the column rules live — which
///         levels earn a column depends on what the viewer holds, so it cannot be settled here.
///     </para>
/// </summary>
/// <param name="Holders">Everyone standing on the band, board players included.</param>
/// <param name="BoardHolders">How many of them the official board is the only record of.</param>
/// <param name="Bands">One per level and type any holder has a chart of, in no particular order.</param>
/// <param name="HoldingByLevel">
///     Per level, how many of them hold at least one chart of it — counting a holder once whatever
///     types they hold it in, which is what the column rule asks about and what summing
///     <see cref="LevelSpreadBand.Holding" /> across types would overstate.
/// </param>
public sealed record CohortLevelSpread(
    int Holders,
    int BoardHolders,
    IReadOnlyList<LevelSpreadBand> Bands,
    IReadOnlyDictionary<int, int> HoldingByLevel)
{
    /// <summary>Nobody stands on the band.</summary>
    public static CohortLevelSpread Empty { get; } =
        new(0, 0, Array.Empty<LevelSpreadBand>(), new Dictionary<int, int>());

    /// <summary>
    ///     The spread of one cohort's pools. A holder the summary has no pool for holds nothing at
    ///     every level rather than dropping out of the count, and a chart <paramref name="charts" />
    ///     does not know — one outside the mix's catalog — counts nowhere.
    /// </summary>
    /// <param name="summary">The cohort and each member's fifty.</param>
    /// <param name="charts">The mix's catalog, which is what levels and types a chart.</param>
    public static CohortLevelSpread Of(PeerPoolSummary summary, IReadOnlyDictionary<Guid, Chart> charts)
    {
        var holders = summary.Peers.ToArray();
        var perHolder = holders
            .Select(p => summary.Pools.TryGetValue(p, out var pool)
                ? CountByLevel(pool, charts)
                : new Dictionary<(int Level, ChartType Type), int>())
            .ToArray();

        var holdingByLevel = new Dictionary<int, int>();
        foreach (var counts in perHolder)
        foreach (var level in counts.Keys.Select(key => key.Level).Distinct())
            holdingByLevel[level] = holdingByLevel.GetValueOrDefault(level) + 1;

        var bands = perHolder.SelectMany(counts => counts.Keys).Distinct()
            .Select(key => Band(key.Level, key.Type, perHolder.Select(c => c.GetValueOrDefault(key)).ToArray()))
            .ToArray();
        return new CohortLevelSpread(holders.Length, holders.Count(p => p.IsFromBoard), bands, holdingByLevel);
    }

    /// <summary>How many charts of each level and type a pool holds.</summary>
    internal static Dictionary<(int Level, ChartType Type), int> CountByLevel(IEnumerable<Guid> pool,
        IReadOnlyDictionary<Guid, Chart> charts)
    {
        return pool.Where(charts.ContainsKey)
            .GroupBy(id => ((int)charts[id].Level, charts[id].Type))
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>One band from the counts of every holder in the cohort, the zeroes among them.</summary>
    internal static LevelSpreadBand Band(int level, ChartType type, int[] counts)
    {
        Array.Sort(counts);
        return new LevelSpreadBand(level, type, counts.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count()),
            counts.Length == 0 ? 0 : counts[0],
            Quantile(counts, 0.25),
            Quantile(counts, 0.5),
            Quantile(counts, 0.75),
            counts.Length == 0 ? 0 : counts[^1],
            counts.Count(c => c > 0));
    }

    /// <summary>
    ///     A quantile of sorted counts by linear interpolation between the closest ranks, so the
    ///     median of an even group sits halfway between its middle two.
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

/// <summary>
///     One column of a spread: a cohort's band with the viewer standing on it
///     (docs/design/pumbility-overhaul.md D68). Where the pool holds both types a level draws one of
///     these per type, and nothing is ever summed across them.
/// </summary>
/// <param name="Level">The chart level the column is for.</param>
/// <param name="Type">The chart type the column counts.</param>
/// <param name="PeersByCount">
///     How many of the cohort hold exactly that many charts of the level and type, keyed by the
///     count — zero included, so the ones holding none are a count like any other.
/// </param>
/// <param name="Fewest">The fewest charts of the level any of them holds.</param>
/// <param name="FirstQuartile">The bottom of the middle half.</param>
/// <param name="Median">The median holder's count.</param>
/// <param name="ThirdQuartile">The top of the middle half.</param>
/// <param name="Most">The most charts of the level any of them holds.</param>
/// <param name="Holding">How many of them hold at least one.</param>
/// <param name="Mine">How many charts of the level and type the viewer's own fifty holds.</param>
/// <param name="PeersBelowMine">How many of them hold fewer than the viewer.</param>
/// <param name="PeersLevelWithMine">How many of them hold exactly as many as the viewer.</param>
public sealed record LevelSpreadColumn(
    int Level,
    ChartType Type,
    IReadOnlyDictionary<int, int> PeersByCount,
    int Fewest,
    double FirstQuartile,
    double Median,
    double ThirdQuartile,
    int Most,
    int Holding,
    int Mine,
    int PeersBelowMine,
    int PeersLevelWithMine);

/// <summary>
///     Where the levels sit (docs/design/pumbility-overhaul.md D68): for the pool in scope, a column
///     per level — per level and type where the pool holds both — of how many charts of it everyone
///     holding the viewer's title keeps in their fifty, with the viewer's own count on it. Pure —
///     the cohort, their pools and the viewer's fifty are the caller's, so the Breakdown page's
///     chart and the probe that mocked it read the same arithmetic.
/// </summary>
/// <param name="Peers">Everyone the spread counts, board players included.</param>
/// <param name="BoardPeers">How many of them the official board is the only record of.</param>
/// <param name="Columns">
///     Lowest level first and singles before doubles, with no gap between the first level and the
///     last.
/// </param>
public sealed record PeerLevelSpread(int Peers, int BoardPeers, IReadOnlyList<LevelSpreadColumn> Columns)
{
    /// <summary>
    ///     A level earns a column when at least one holder in this many has a chart of it, or the
    ///     viewer does. Below that a column is a handful of outliers drawn as though they were a
    ///     folder the cohort plays.
    /// </summary>
    public const int ColumnShare = 50;

    /// <summary>Nothing to draw at all.</summary>
    public static PeerLevelSpread Empty { get; } = new(0, 0, Array.Empty<LevelSpreadColumn>());

    /// <summary>
    ///     The viewer placed on a cohort's spread. Which levels earn a column is settled here rather
    ///     than in <see cref="CohortLevelSpread" />, because the viewer's own fifty earns one too.
    /// </summary>
    /// <param name="cohort">The band's own spread, read once and shared by everyone on it.</param>
    /// <param name="charts">The mix's catalog, which is what levels and types a chart.</param>
    /// <param name="mine">The charts in the viewer's own fifty of the pool.</param>
    public static PeerLevelSpread Of(CohortLevelSpread cohort, IReadOnlyDictionary<Guid, Chart> charts,
        IEnumerable<Guid> mine)
    {
        var myCounts = CohortLevelSpread.CountByLevel(mine, charts);
        var bands = cohort.Bands.ToDictionary(band => (band.Level, band.Type));

        // A mixed pool draws both types at every level it draws, so a level where the cohort keeps
        // singles and the viewer keeps doubles reads as the two different answers it is.
        var types = bands.Keys.Select(key => key.Type).Concat(myCounts.Keys.Select(key => key.Type))
            .Distinct().OrderBy(type => type).ToArray();
        var shown = cohort.HoldingByLevel.Where(kv => kv.Value * ColumnShare >= cohort.Holders)
            .Select(kv => kv.Key)
            .Concat(myCounts.Keys.Select(key => key.Level))
            .ToArray();
        if (shown.Length == 0 || types.Length == 0)
            return new PeerLevelSpread(cohort.Holders, cohort.BoardHolders, Array.Empty<LevelSpreadColumn>());

        var lowest = shown.Min();
        var columns = Enumerable.Range(lowest, shown.Max() - lowest + 1)
            .SelectMany(level => types.Select(type => Column(level, type, bands.GetValueOrDefault((level, type)),
                cohort.Holders, myCounts.GetValueOrDefault((level, type)))))
            .ToArray();
        return new PeerLevelSpread(cohort.Holders, cohort.BoardHolders, columns);
    }

    /// <summary>The spread of one cohort's pools in one pass, for a caller with no band to cache.</summary>
    public static PeerLevelSpread Of(PeerPoolSummary summary, IReadOnlyDictionary<Guid, Chart> charts,
        IEnumerable<Guid> mine)
    {
        return Of(CohortLevelSpread.Of(summary, charts), charts, mine);
    }

    /// <summary>
    ///     A column from the cohort's band and the viewer's own count. A level and type nobody in the
    ///     cohort holds has no band of its own, and every one of them holds none of it.
    /// </summary>
    private static LevelSpreadColumn Column(int level, ChartType type, LevelSpreadBand? band, int holders, int mine)
    {
        band ??= CohortLevelSpread.Band(level, type, new int[holders]);
        return new LevelSpreadColumn(level, type, band.HoldersByCount, band.Fewest, band.FirstQuartile,
            band.Median, band.ThirdQuartile, band.Most, band.Holding, mine,
            band.HoldersByCount.Where(kv => kv.Key < mine).Sum(kv => kv.Value),
            band.HoldersByCount.GetValueOrDefault(mine));
    }
}
