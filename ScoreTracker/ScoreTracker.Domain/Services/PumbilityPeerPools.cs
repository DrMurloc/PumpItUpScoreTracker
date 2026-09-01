using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     What a peer group's PUMBILITY pools are made of, from the peers' records
///     (docs/design/pumbility-overhaul.md §3.10, D33; both mixes since D43 — the scoring handed in
///     is what makes it Phoenix 1 or Phoenix 2). Pure: hand it the records, the peers and the
///     catalog, and it hands back <see cref="PeerPoolSummary" />.
///     <para>
///         A peer's pool is their fifty highest-priced non-broken records of the type — the same
///         rule the tier lists' nightly writer applies — and a chart's <b>prevalence</b> is a Borda
///         count over those pools: the chart at a peer's #1 scores 50, at #50 scores 1, summed over
///         the peers who hold it. Every peer casts the same 1,275 points, so a strong peer cannot
///         outvote a weak one the way a raw value sum lets them. The score statistics are read
///         over every peer who scored the chart, holders or not, with the estimator's own
///         quantile arithmetic and its five-peer floor, so a page's median and the projection's
///         estimate are the same number.
///     </para>
/// </summary>
public static class PumbilityPeerPools
{
    /// <summary>Slots in a pool, and the points its first slot is worth.</summary>
    public const int PoolSize = PeerGroup.PumbilityPoolSize;

    /// <summary>
    ///     Peers who must have scored a chart before its median means anything (D24). The same
    ///     number the projection's own floor starts at, but held here rather than relaxed with it:
    ///     where a band is too thin to meet the floor the projection answers on what it has (D47)
    ///     and this does not, so a row can carry a gain and still say "Fewer than 5 peers scored
    ///     it" beside a blank IQR. That pairing IS the disclaimer — the number is the page's
    ///     stricter statement about the evidence, not a disagreement with the projection.
    /// </summary>
    public const int MinimumScored = PeerEstimator.Phoenix2MinimumPeers;

    /// <summary>
    ///     Builds the summary. Records of players outside <paramref name="peers" /> and records of
    ///     charts outside <paramref name="charts" /> are ignored; a record that prices at zero
    ///     (a broken run, a sub-10 chart) can hold no pool slot and is left out of the pools, but
    ///     it is not a score anyway — <paramref name="records" /> are the peers' non-broken bests.
    /// </summary>
    public static PeerPoolSummary Build(IEnumerable<UserPhoenixScore> records, IReadOnlySet<Guid> peers,
        IReadOnlyDictionary<Guid, Chart> charts, ScoringConfiguration scoring)
    {
        var byPeer = new Dictionary<Guid, List<(Guid ChartId, double Rating, int Score)>>();
        var scores = new Dictionary<Guid, List<int>>();
        foreach (var record in records)
        {
            if (!peers.Contains(record.UserId) || !charts.TryGetValue(record.ChartId, out var chart)) continue;
            if (!scores.TryGetValue(record.ChartId, out var voices)) scores[record.ChartId] = voices = new List<int>();
            voices.Add((int)record.Score);

            var rating = scoring.GetScore(chart, record.Score, record.Plate ?? PhoenixPlate.RoughGame, record.IsBroken);
            if (rating <= 0) continue;
            if (!byPeer.TryGetValue(record.UserId, out var priced))
                byPeer[record.UserId] = priced = new List<(Guid, double, int)>();
            priced.Add((record.ChartId, rating, (int)record.Score));
        }

        var pools = new Dictionary<Guid, IReadOnlySet<Guid>>();
        var holders = new Dictionary<Guid, int>();
        var points = new Dictionary<Guid, int>();
        foreach (var (peer, priced) in byPeer)
        {
            var pool = priced.OrderByDescending(p => p.Rating).ThenBy(p => p.ChartId).Take(PoolSize).ToArray();
            pools[peer] = pool.Select(p => p.ChartId).ToHashSet();
            for (var slot = 0; slot < pool.Length; slot++)
            {
                holders[pool[slot].ChartId] = holders.GetValueOrDefault(pool[slot].ChartId) + 1;
                points[pool[slot].ChartId] = points.GetValueOrDefault(pool[slot].ChartId) + (PoolSize - slot);
            }
        }

        // A peer who never scored anything priceable still counts as a peer with an empty pool:
        // a Phoenix 2 band admits only full pools so it cannot happen there, a competitive band
        // has no such rule (D43) — either way the summary must not invent a pool for them.
        foreach (var peer in peers) pools.TryAdd(peer, new HashSet<Guid>());

        var summary = new Dictionary<Guid, PeerPoolChart>();
        foreach (var chartId in holders.Keys.Concat(scores.Keys).Distinct())
        {
            var voices = scores.TryGetValue(chartId, out var v) ? v : new List<int>();
            var held = holders.GetValueOrDefault(chartId);
            if (held == 0 && voices.Count < MinimumScored) continue;

            var scored = voices.Select(s => new PeerScore(s, 0, 0)).ToArray();
            var median = PeerEstimator.Estimate(scored, 0, PeerEstimator.Median, MinimumScored);
            var q1 = median == null ? null : PeerEstimator.Estimate(scored, 0, PeerEstimator.LowerQuartile, MinimumScored);
            var q3 = median == null ? null : PeerEstimator.Estimate(scored, 0, PeerEstimator.UpperQuartile, MinimumScored);
            voices.Sort();
            summary[chartId] = new PeerPoolChart(held, points.GetValueOrDefault(chartId), voices.Count,
                median == null ? null : PhoenixScore.From(median.Value),
                q1 == null ? null : PhoenixScore.From(q1.Value),
                q3 == null ? null : PhoenixScore.From(q3.Value),
                voices);
        }

        return new PeerPoolSummary(peers, pools, summary);
    }
}
