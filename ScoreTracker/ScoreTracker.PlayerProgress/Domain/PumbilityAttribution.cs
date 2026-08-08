namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     Splits a batch's PUMBILITY movement across the charts that caused it.
///     <para>
///         The crown on a session row says a chart sits in your top 50 — a standing fact. A chart
///         can sit there all night and have gained you nothing. This answers the other question:
///         what did tonight's play on it actually add to your total.
///     </para>
///     <para>
///         Only charts whose score changed can gain, and a pool of fixed size means entrants and
///         leavers always arrive in equal numbers — so the split is exact and sums to the pool's
///         real movement, whatever order the pairing takes. The pairing chosen is the order the
///         pool actually falls in: the strongest entrant displaces the weakest seat, because
///         adding the best new chart first is what pushes out the fiftieth.
///     </para>
/// </summary>
internal static class PumbilityAttribution
{
    /// <summary>
    ///     One chart's pool value on both sides of the batch. <paramref name="Old" /> is null when
    ///     no score stood here before — the chart could not have held a seat.
    /// </summary>
    internal sealed record Priced(Guid ChartId, double? Old, double New);

    /// <summary>
    ///     Gain per chart, rounded to whole PUMBILITY and keyed only by the charts that gained.
    ///     A chart that lost ground, or moved by less than a point, is absent rather than zero —
    ///     the caller renders a badge for exactly what this returns.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> GainsPerChart(IReadOnlyList<Priced> priced, int poolSize)
    {
        var newPool = priced.OrderByDescending(p => p.New).Take(poolSize).ToArray();
        var oldPool = priced.Where(p => p.Old != null)
            .OrderByDescending(p => p.Old!.Value)
            .Take(poolSize)
            .ToArray();
        var heldBefore = oldPool.Select(p => p.ChartId).ToHashSet();
        var heldAfter = newPool.Select(p => p.ChartId).ToHashSet();

        var gains = new Dictionary<Guid, int>();

        // Kept its seat and improved: the whole improvement reached your total, because nothing
        // had to leave to make room for it.
        foreach (var held in newPool.Where(p => heldBefore.Contains(p.ChartId)))
            Record(gains, held.ChartId, held.New - held.Old!.Value);

        // Took a seat: worth the difference between it and what it pushed out.
        var entrants = newPool.Where(p => !heldBefore.Contains(p.ChartId))
            .OrderByDescending(p => p.New)
            .ToArray();
        var leavers = oldPool.Where(p => !heldAfter.Contains(p.ChartId))
            .OrderBy(p => p.Old!.Value)
            .ToArray();

        for (var i = 0; i < entrants.Length; i++)
            // Past the leavers the pool simply was not full yet, so the entrant displaced
            // nothing and every point it brought is new.
            Record(gains, entrants[i].ChartId,
                i < leavers.Length ? entrants[i].New - leavers[i].Old!.Value : entrants[i].New);

        return gains;
    }

    private static void Record(IDictionary<Guid, int> gains, Guid chartId, double gain)
    {
        var whole = (int)Math.Round(gain);
        if (whole > 0) gains[chartId] = whole;
    }
}
