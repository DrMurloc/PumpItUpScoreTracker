namespace ScoreTracker.Domain.Services;

/// <summary>
///     One peer's score on the chart being estimated, with everything needed to weigh it:
///     the level they hold now, and the level they held when they set it.
/// </summary>
public readonly record struct PeerScore(int Score, double LevelNow, double LevelWhenSet)
{
    /// <summary>How much this peer has grown since setting it. Never negative — a player whose
    ///     level fell has not made the score less representative of them.</summary>
    public double Growth => Math.Max(0.0, LevelNow - LevelWhenSet);
}

/// <summary>
///     The score estimator (docs/design/pumbility-overhaul.md §4.1). Pure — no I/O, no clock,
///     no randomness — so the exploration harness and every shipping caller run the same
///     arithmetic and cannot drift.
///     <para>
///         Given the peers inside a competitive band who have played a chart, it answers
///         "what would a player at this level score here": each peer's score discounted by how
///         much they have grown since setting it, then a weighted quantile of what remains.
///     </para>
///     <para>
///         It deliberately takes no argument describing the player being predicted for. Four
///         attempts to personalize beyond competitive level — the chabala skill nudge,
///         chart-similarity residual transfer, skill-thumbprint matching, and direct
///         score-pattern matching — each measured at or under 0.3% and are recorded as rejected
///         in §4.3. The peers' scores already encode who the chart suits.
///     </para>
/// </summary>
public static class CohortEstimator
{
    /// <summary>
    ///     Growth decay, in competitive levels. A peer who has not moved counts at full voice;
    ///     one who has gained two levels counts at about an eighth. Self-conditioning: it needs
    ///     no threshold and no "was this player improving" detector, because a stable player's
    ///     growth is zero and the weight is 1.0 for their whole record (§4.2c).
    /// </summary>
    public const double GrowthDecayLevels = 1.0;

    /// <summary>
    ///     Which quantile of the peer distribution to read off. NOT the mean: per-chart scores
    ///     are left-skewed by a tail of barely-passed attempts, and the mean sits in that tail —
    ///     measured, a mean carries −8,319 bias where this carries +180.
    ///     <para>
    ///         ⚠ Fitted against a ONE-YEAR truth horizon and re-fittable by design. It is the
    ///         same species of constant as the ×0.95 fudge it replaces, and it moves bias by
    ///         ~5,000 points across its useful range. If a caller's claim changes from "what
    ///         you would eventually score" toward "what you would score today", this drops.
    ///     </para>
    /// </summary>
    public const double Quantile = 0.65;

    /// <summary>
    ///     Competitive-level half-width of the peer gate for the PUMBILITY projection, measured
    ///     optimal for that page's job — predicting the score itself, where ±0.5 costs 3.1%,
    ///     ±2.0 costs 3.1% and ±3.0 costs 12.4% of accuracy (§4.5).
    ///     <para>
    ///         It is not a site-wide rule, and callers pass their own. A tier list only needs
    ///         the folder's charts ranked against each other rather than the number quoted, and
    ///         the rest of the site calls a competitive peer ±0.5, so it asks for ±0.5.
    ///     </para>
    /// </summary>
    public const double CompetitiveWindow = 1.0;

    /// <summary>
    ///     The estimate, or null when no peer has played the chart. Null means "no opinion" and
    ///     callers must render it as such — a fabricated number here is the failure mode the
    ///     old estimator's silent gates produced.
    /// </summary>
    public static int? Estimate(IReadOnlyCollection<PeerScore> peers,
        double growthDecayLevels = GrowthDecayLevels, double quantile = Quantile)
    {
        if (peers.Count == 0) return null;

        var weighted = peers
            .Select(p => (Value: (double)p.Score, Weight: GrowthWeight(p.Growth, growthDecayLevels)))
            .Where(p => p.Weight > 0)
            .OrderBy(p => p.Value)
            .ToArray();
        if (weighted.Length == 0) return null;

        return (int)Math.Round(WeightedQuantile(weighted, quantile));
    }

    /// <summary>
    ///     exp(−growth / decay). Public so the exploration harness can measure the weighting
    ///     independently of the estimate it feeds.
    /// </summary>
    public static double GrowthWeight(double growth, double decayLevels = GrowthDecayLevels)
    {
        if (decayLevels <= 0) return 1.0;
        return Math.Exp(-Math.Max(0.0, growth) / decayLevels);
    }

    /// <summary>
    ///     Linear-interpolated weighted quantile over values already sorted ascending. Uses the
    ///     midpoint convention (cumulative weight minus half the current weight), so a single
    ///     observation returns itself at every quantile rather than snapping to an endpoint.
    /// </summary>
    private static double WeightedQuantile((double Value, double Weight)[] sorted, double quantile)
    {
        var total = sorted.Sum(s => s.Weight);
        if (total <= 0) return sorted[^1].Value;

        var q = Math.Clamp(quantile, 0.0, 1.0);
        var cumulative = 0.0;
        var positions = new double[sorted.Length];
        for (var i = 0; i < sorted.Length; i++)
        {
            cumulative += sorted[i].Weight;
            positions[i] = (cumulative - 0.5 * sorted[i].Weight) / total;
        }

        if (q <= positions[0]) return sorted[0].Value;
        if (q >= positions[^1]) return sorted[^1].Value;

        for (var i = 1; i < sorted.Length; i++)
        {
            if (q > positions[i]) continue;
            var span = positions[i] - positions[i - 1];
            if (span <= 0) return sorted[i].Value;
            var t = (q - positions[i - 1]) / span;
            return sorted[i - 1].Value + t * (sorted[i].Value - sorted[i - 1].Value);
        }

        return sorted[^1].Value;
    }
}
