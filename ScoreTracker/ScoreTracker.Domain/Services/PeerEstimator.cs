using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;

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
///     The score estimator (docs/design/pumbility-overhaul.md §4.1 on Phoenix 1, §4.8 on
///     Phoenix 2). Pure — no I/O, no clock, no randomness — so the exploration harness and
///     every shipping caller run the same arithmetic and cannot drift.
///     <para>
///         Given the peers who have played a chart, it answers "what would a player like this
///         score here": each peer's score discounted by how much they have grown since setting
///         it, then a weighted quantile of what remains. Who the peers are is the caller's
///         business — a competitive band on Phoenix 1, PUMBILITY peers on Phoenix 2 — and so
///         is the quantile and whether growth is weighed at all; this class only does the sum.
///     </para>
///     <para>
///         It deliberately takes no argument describing the player being predicted for. Four
///         attempts to personalize beyond the peer group — the chabala skill nudge,
///         chart-similarity residual transfer, skill-thumbprint matching, and direct
///         score-pattern matching — each measured at or under 0.3% and are recorded as rejected
///         in §4.3. The peers' scores already encode who the chart suits.
///     </para>
/// </summary>
public static class PeerEstimator
{
    /// <summary>
    ///     Growth decay, in competitive levels. A peer who has not moved counts at full voice;
    ///     one who has gained two levels counts at about an eighth. Self-conditioning: it needs
    ///     no threshold and no "was this player improving" detector, because a stable player's
    ///     growth is zero and the weight is 1.0 for their whole record (§4.2c). Zero or below
    ///     turns the weighting off — every score at full voice — which is what Phoenix 2 asks
    ///     for (§4.8: its levels climbed three rungs in the first month, so the discount was
    ///     silencing more than half of the evidence).
    /// </summary>
    public const double GrowthDecayLevels = 1.0;

    /// <summary>
    ///     Which quantile of the peer distribution a projection reads by default, on both mixes:
    ///     the median (docs/design/pumbility-overhaul.md D54, §4.11) — Great on the PUMBILITY
    ///     page's Energy select, and the rung every surface off that page reads. NOT the mean:
    ///     per-chart scores are left-skewed by a tail of barely-passed attempts and the mean sits
    ///     in that tail.
    ///     <para>
    ///         Round seven read the first quartile (D50): on the ±3-rung band the top of a
    ///         gain-sorted list — selected for the charts whose estimate ran high — read +4,728 at
    ///         the median with its SS calls right half the time. Round eight replaced the band with
    ///         the window on the pool of the type (D53), under which the same top ten reads −1,611
    ///         at the median, and the owner set Great as the default to field-test it. Phoenix 1
    ///         read the 65th percentile until round seven — measured +7,359 against frozen records —
    ///         and Phoenix 2 the median (the retired D26). Surfaces that let the player choose ask
    ///         for other rungs through <see cref="ScoreProjectionRequest.Quantiles" />; everything
    ///         else reads this one.
    ///     </para>
    /// </summary>
    public const double DefaultQuantile = Median;

    /// <summary>The median — the middle of the peers, the rung a page's "Great" reads, and the default (D54).</summary>
    public const double Median = 0.50;

    /// <summary>
    ///     How many peers Phoenix 2 requires before it holds an opinion on a chart (§4.8, D24).
    ///     Below it the chart is not shown at all. Phoenix 1 keeps the default of one — its
    ///     coverage was measured as the transformative part of the estimator (§4.2), and a
    ///     Phoenix 1 peer group is hundreds of players.
    /// </summary>
    public const int Phoenix2MinimumPeers = 5;

    /// <summary>
    ///     Competitive-level half-width of the peer gate for the PUMBILITY projection on
    ///     Phoenix 1, measured optimal for that page's job — predicting the score itself, where
    ///     ±0.5 costs 3.1%, ±2.0 costs 3.1% and ±3.0 costs 12.4% of accuracy (§4.5).
    ///     <para>
    ///         It is not a site-wide rule, and callers pass their own. A tier list only needs
    ///         the folder's charts ranked against each other rather than the number quoted, and
    ///         the rest of the site calls a competitive peer ±0.5, so it asks for ±0.5.
    ///     </para>
    /// </summary>
    public const double CompetitiveWindow = 1.0;

    /// <summary>
    ///     The estimate, or null when too few peers have played the chart — fewer than
    ///     <paramref name="minimumPeers" />, which is one unless the caller says otherwise. Null
    ///     means "no opinion" and callers must render it as such — a fabricated number here is
    ///     the failure mode the old estimator's silent gates produced.
    /// </summary>
    public static int? Estimate(IReadOnlyCollection<PeerScore> peers,
        double growthDecayLevels = GrowthDecayLevels, double quantile = DefaultQuantile, int minimumPeers = 1)
    {
        var weighted = Weigh(peers, growthDecayLevels, minimumPeers);
        return weighted == null ? null : (int)Math.Round(WeightedQuantile(weighted, quantile));
    }

    /// <summary>
    ///     Several quantiles of the same peers at once — the same voices, the same growth weights,
    ///     the same arithmetic as <see cref="Estimate" /> at each rung — with the peer count, or
    ///     null under the floor exactly where <see cref="Estimate" /> is. What a caller that lets
    ///     the player choose a rung caches, so the choice is a lookup rather than a second sweep.
    /// </summary>
    public static PeerLadder? Ladder(IReadOnlyCollection<PeerScore> peers, IReadOnlyCollection<double> quantiles,
        double growthDecayLevels = GrowthDecayLevels, int minimumPeers = 1)
    {
        var weighted = Weigh(peers, growthDecayLevels, minimumPeers);
        if (weighted == null) return null;

        var rungs = new Dictionary<double, PhoenixScore>();
        foreach (var quantile in quantiles)
            rungs[quantile] = PhoenixScore.From((int)Math.Round(WeightedQuantile(weighted, quantile)));
        return new PeerLadder(rungs, peers.Count);
    }

    /// <summary>The voices in reading order with their weights, or null when there are too few to hold an opinion.</summary>
    private static (double Value, double Weight)[]? Weigh(IReadOnlyCollection<PeerScore> peers,
        double growthDecayLevels, int minimumPeers)
    {
        if (peers.Count < Math.Max(1, minimumPeers)) return null;

        var weighted = peers
            .Select(p => (Value: (double)p.Score, Weight: GrowthWeight(p.Growth, growthDecayLevels)))
            .Where(p => p.Weight > 0)
            .OrderBy(p => p.Value)
            .ToArray();
        return weighted.Length == 0 ? null : weighted;
    }

    /// <summary>The quartiles — the same quantile arithmetic at 25 and 75: Good and Top of my game on the page's select.</summary>
    public const double LowerQuartile = 0.25;

    public const double UpperQuartile = 0.75;

    /// <summary>
    ///     exp(−growth / decay). Public so the exploration harness can measure the weighting
    ///     independently of the estimate it feeds. A decay of zero or below is "off": 1.0.
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
