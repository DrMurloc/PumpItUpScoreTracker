using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     Which charts a folder gives up to Hardmode, and in what order
///     (docs/design/hardmode-leaderboard.md §1).
/// </summary>
internal static class HardmodeCut
{
    /// <summary>A pool is fifty, so a chart at slot 1 is worth 50 and slot 50 is worth 1.</summary>
    public const int PoolSize = 50;

    /// <summary>The flat cut a folder gives up before the quarter rule or the unheld rule move it.</summary>
    public const int FlatCut = 25;

    /// <summary>
    ///     A folder this small or smaller gives up exactly one chart. Without it ⌊2 ÷ 4⌋ = 0
    ///     excluded S26 entirely, which is to say it excluded 1948 (D2).
    /// </summary>
    public const int TinyFolder = 4;

    /// <summary>What one chart's place in one pool is worth to it.</summary>
    public static int SlotWeight(int slot)
    {
        return PoolSize + 1 - slot;
    }

    /// <summary>
    ///     How many charts this folder gives up: twenty-five, or a quarter of itself if that is
    ///     fewer, or every chart no full pool holds when there are more of those than either — and
    ///     exactly one from a folder of four or fewer (D1, D2).
    /// </summary>
    public static int CutSize(int folderSize, int unheld)
    {
        if (folderSize <= 0) return 0;
        if (folderSize <= TinyFolder) return 1;
        return Math.Max(Math.Min(FlatCut, folderSize / 4), Math.Min(unheld, folderSize));
    }

    /// <summary>
    ///     The folder in cut order — rarest first: weighted points ascending, then scoring level
    ///     descending, then name (D3).
    ///     <para>
    ///         The scoring-level tiebreak only decides charts whose weighted points are exactly
    ///         equal, which is rare; it exists so the list is deterministic week to week. 94
    ///         doubles charts and 19 singles at level 20+ carry no usable scoring level, so a
    ///         missing one falls through to the name rather than sorting the chart last.
    ///     </para>
    /// </summary>
    public static IReadOnlyList<HardmodeCandidate> InCutOrder(IEnumerable<HardmodeCandidate> folder)
    {
        return folder
            .OrderBy(c => c.Points)
            .ThenByDescending(c => c.ScoringLevel ?? c.Level)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    ///     One folder's qualifying charts, in cut order. The whole rule in one call, so the census
    ///     and its tests cannot disagree about it.
    /// </summary>
    public static IReadOnlyList<HardmodeCandidate> Qualifying(IEnumerable<HardmodeCandidate> folder)
    {
        var ordered = InCutOrder(folder);
        var unheld = ordered.Count(c => c.Holders == 0);
        return ordered.Take(CutSize(ordered.Count, unheld)).ToArray();
    }
}

/// <summary>
///     A chart competing for its folder's Hardmode slots. <see cref="Points" /> is the weighted
///     hold count and <see cref="Holders" /> counts each player once, so a chart that three of one
///     player's pools hold carries three slot weights and one holder.
/// </summary>
internal sealed record HardmodeCandidate(Guid ChartId, string Name, ChartType ChartType, int Level,
    double? ScoringLevel, double Points, int Holders);
