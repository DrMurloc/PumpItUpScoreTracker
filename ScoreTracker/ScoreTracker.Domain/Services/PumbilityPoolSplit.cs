using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Services;

/// <summary>One priced record of a player's, as the merge sees it: its chart type and what it is worth.</summary>
public readonly record struct PricedRecord(ChartType Type, double Rating);

/// <summary>
///     A merged fifty split by chart type (docs/design/pumbility-overhaul.md D58): how many of
///     the fifty are singles and how many doubles, and what each type contributes. For one player
///     the counts are whole; for an average over peers they are means, and <see cref="Peers" />
///     says over how many.
/// </summary>
public sealed record PoolTypeSplit(int Peers, double SinglesCount, double DoublesCount, double SinglesValue,
    double DoublesValue)
{
    public double Total => SinglesValue + DoublesValue;

    /// <summary>How many slots the fifty holds — fifty for a full pool, fewer while it fills.</summary>
    public double Count => SinglesCount + DoublesCount;
}

/// <summary>
///     The merged fifty and its split by type — a player's records of both types priced, the top
///     fifty taken across them — and the average of that over a set of peers. Pure: the reads and
///     the pricing are the caller's, so the Breakdown page's second bar and the probe that mocked
///     it cannot disagree about the arithmetic.
/// </summary>
public static class PumbilityPoolSplit
{
    /// <summary>
    ///     A player's merged fifty: the highest-priced records above zero across both types, split
    ///     by type. A pool short of fifty is still answered — whether it counts is the caller's
    ///     call, through <see cref="IsFull" />.
    /// </summary>
    public static PoolTypeSplit Of(IEnumerable<PricedRecord> records)
    {
        var fifty = records
            .Where(r => r.Rating > 0)
            .OrderByDescending(r => r.Rating)
            .Take(PeerGroup.PumbilityPoolSize)
            .ToArray();
        return new PoolTypeSplit(1,
            fifty.Count(r => r.Type == ChartType.Single),
            fifty.Count(r => r.Type == ChartType.Double),
            fifty.Where(r => r.Type == ChartType.Single).Sum(r => r.Rating),
            fifty.Where(r => r.Type == ChartType.Double).Sum(r => r.Rating));
    }

    /// <summary>
    ///     Whether a merged fifty is full — the only kind that averages honestly. Half a pool is
    ///     half a vote, the same rule the peers' own pools are held to (D28, D48).
    /// </summary>
    public static bool IsFull(PoolTypeSplit split)
    {
        return split.Count >= PeerGroup.PumbilityPoolSize;
    }

    /// <summary>
    ///     The average split over the players holding a full merged fifty, counted in
    ///     <see cref="PoolTypeSplit.Peers" />; null when none of them does.
    /// </summary>
    public static PoolTypeSplit? Average(IEnumerable<IEnumerable<PricedRecord>> players)
    {
        var full = players.Select(Of).Where(IsFull).ToArray();
        if (full.Length == 0) return null;
        return new PoolTypeSplit(full.Length,
            full.Average(s => s.SinglesCount),
            full.Average(s => s.DoublesCount),
            full.Average(s => s.SinglesValue),
            full.Average(s => s.DoublesValue));
    }
}
