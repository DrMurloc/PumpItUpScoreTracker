using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application
{
    /// <summary>
    ///     Projects what a player would score on charts they have not played, and what that
    ///     would add to their PUMBILITY (docs/design/pumbility-overhaul.md §4.1 on Phoenix 1,
    ///     §4.8 on Phoenix 2).
    ///     <para>
    ///         The projection itself is <see cref="IScoreProjector" />, shared with the
    ///         personalized Score tier list so the two surfaces cannot answer "what would you
    ///         score here" differently. What is left here is this page's own half: which charts
    ///         are worth asking about, and what the answers are worth against the player's own
    ///         top-50 bar.
    ///     </para>
    /// </summary>
    internal sealed class PumbilityProjectionSaga : IRequestHandler<ProjectPumbilityGainsQuery, PumbilityProjection>,
        IRequestHandler<GetPumbilityPeersPageQuery, PumbilityPeersPageRecord>,
        IRequestHandler<GetPumbilityPeersQuery, IReadOnlyCollection<Guid>>
    {
        /// <summary>
        ///     Phoenix 1: how far a chart's scoring level may sit from the player's competitive
        ///     level and still be worth projecting. Beyond this the estimate is arithmetically
        ///     fine and practically useless — nobody grinding 21s needs a number for a 26.
        ///     Phoenix 2 has no window (D24): its peers are drawn on the PUMBILITY ladder, not on a
        ///     level, and the five-peer floor is what keeps an unrealistic chart off the list.
        /// </summary>
        private const double ScoringLevelWindow = 2.0;

        /// <summary>How many peer-estimated suggestions survive to the merge (owner, 2026-08-06).</summary>
        private const int MaxTargets = 100;

        /// <summary>The list name the prevalence tiers are banded under.</summary>
        internal const string PrevalenceListName = "Prevalence";

        private readonly PumbilityProjectionCache _cache;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMediator _mediator;
        private readonly IScoreProjector _projector;
        private readonly IScoreReader _scores;
        private readonly IPlayerStatsReader _stats;
        private readonly IUserReader _users;

        public PumbilityProjectionSaga(IMediator mediator, IScoreProjector projector,
            PumbilityProjectionCache cache, IScoreReader scores, IPlayerStatsReader stats, IUserReader users,
            ICurrentUserAccessor currentUser)
        {
            _mediator = mediator;
            _projector = projector;
            _cache = cache;
            _scores = scores;
            _stats = stats;
            _users = users;
            _currentUser = currentUser;
        }

        public async Task<PumbilityProjection> Handle(ProjectPumbilityGainsQuery request,
            CancellationToken cancellationToken)
        {
            // Two halves with nothing in common. The estimates are the peer sweep — sized by
            // the player population, the same for all three pools, and unchanged by anything
            // the viewer does, since a player's own scores never enter their own peer group. The
            // pricing is arithmetic over their top hundred, and moves the moment they play.
            // So one is held for a day and the other is redone on every visit.
            var sweep = await _cache.GetOrAdd(request.UserId, request.Mix,
                () => Estimate(request.UserId, request.Mix));
            return await Price(sweep, request, cancellationToken);
        }

        /// <summary>
        ///     The Play page (docs/design/pumbility-overhaul.md §3.10): the peers' pools out of the
        ///     cached sweep — the PUMBILITY band on Phoenix 2, the competitive band on Phoenix 1 (D43)
        ///     — tiered by prevalence per type, with the viewer's own pool and scores laid over them,
        ///     and the roster. A viewer with no lit type answers empty — the page prints the dark
        ///     chips from the group record either way.
        /// </summary>
        public async Task<PumbilityPeersPageRecord> Handle(GetPumbilityPeersPageQuery request,
            CancellationToken cancellationToken)
        {
            var (userId, mix, pool) = request;

            var sweep = await _cache.GetOrAdd(userId, mix, () => Estimate(userId, mix));
            var types = pool is { } only ? new[] { only } : new[] { ChartType.Single, ChartType.Double };
            var groups = types.Where(sweep.Peers.ContainsKey).ToDictionary(t => t, t => sweep.Peers[t]);
            var lit = types.Where(sweep.PeerPools.ContainsKey).ToArray();
            if (lit.Length == 0)
                return PumbilityPeersPageRecord.Empty(mix, pool) with { Peers = groups };

            var charts = (await _mediator.Send(new GetChartsQuery(mix), cancellationToken)).ToDictionary(c => c.Id);
            var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
            var mine = (await _scores.GetBestScores(mix, userId, cancellationToken))
                .Where(r => r.Score != null && !r.IsBroken && charts.ContainsKey(r.ChartId))
                .ToDictionary(r => r.ChartId);

            var entries = new List<PeerPoolEntry>();
            var alone = new List<PeerAloneEntry>();
            var compare = new Dictionary<ChartType, PeerCompare>();
            var myPools = new Dictionary<ChartType, IReadOnlyDictionary<Guid, int>>();
            foreach (var type in lit)
            {
                var summary = sweep.PeerPools[type];
                var peerCount = summary.PeerIds.Count;

                // The viewer's own pool of the type, by the same rule the peers' were built with:
                // the fifty highest-priced non-broken records above zero, ranked.
                var myPool = mine.Values
                    .Where(r => charts[r.ChartId].Type == type)
                    .Select(r => (r.ChartId, Rating: scoring.GetScore(charts[r.ChartId], r.Score!.Value,
                        r.Plate ?? PhoenixPlate.RoughGame, r.IsBroken)))
                    .Where(r => r.Rating > 0)
                    .OrderByDescending(r => r.Rating).ThenBy(r => r.ChartId)
                    .Take(PumbilityPeerPools.PoolSize)
                    .ToArray();
                var myRank = myPool.Select((r, i) => (r.ChartId, Rank: i + 1)).ToDictionary(r => r.ChartId, r => r.Rank);
                myPools[type] = myRank;

                var held = summary.Charts.Where(kv => kv.Value.Holders > 0).ToDictionary(kv => kv.Key, kv => kv.Value.Points);
                var tiers = TierListProcessor.ProcessIntoLogScaledTierList(PrevalenceListName, held)
                    .ToDictionary(e => e.ChartId);
                var variability = PeerVariability.Band(summary.Charts
                    .Where(kv => kv.Value.Median != null)
                    .Select(kv => (kv.Key, kv.Value.Quartile1!.Value, kv.Value.Quartile3!.Value)));

                foreach (var (chartId, chart) in summary.Charts)
                {
                    if (chart.Holders == 0) continue;
                    var tier = tiers[chartId];
                    var myScore = mine.TryGetValue(chartId, out var record) ? record.Score : null;
                    entries.Add(new PeerPoolEntry(chartId, type, chart.Holders, peerCount, chart.Points, tier.Category,
                        tier.Order, chart.Scored, chart.Median, chart.Quartile1, chart.Quartile3,
                        variability.TryGetValue(chartId, out var level) ? level : null,
                        myRank.TryGetValue(chartId, out var rank) ? rank : null,
                        myScore, record?.Plate,
                        myScore is { } s ? chart.PercentileOf((int)s) : null));
                }

                foreach (var (chartId, rating) in myPool)
                {
                    if (summary.Charts.TryGetValue(chartId, out var c) && c.Holders > 0) continue;
                    var record = mine[chartId];
                    alone.Add(new PeerAloneEntry(chartId, type, myRank[chartId], record.Score!.Value, record.Plate, rating));
                }

                compare[type] = CompareWith(summary, myPool.Select(r => r.ChartId).ToArray(), charts);
            }

            var (roster, privatePeers, you) = await Roster(mix, userId, lit, sweep, myPools, cancellationToken);
            return new PumbilityPeersPageRecord(mix, pool, groups, entries, alone, roster, privatePeers, you, compare);
        }

        /// <summary>
        ///     The current user's peers of a type, out of the cached sweep — the PUMBILITY band on
        ///     Phoenix 2, the competitive band on Phoenix 1 (D43) — empty for a dark type or for
        ///     nobody signed in.
        /// </summary>
        public async Task<IReadOnlyCollection<Guid>> Handle(GetPumbilityPeersQuery request,
            CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) return Array.Empty<Guid>();
            var userId = _currentUser.User.Id;
            var sweep = await _cache.GetOrAdd(userId, request.Mix, () => Estimate(userId, request.Mix));
            return sweep.PeerPools.TryGetValue(request.ChartType, out var summary)
                ? summary.PeerIds.ToArray()
                : Array.Empty<Guid>();
        }

        /// <summary>
        ///     Where the viewer's pool of one type sits against the peers' by level (D41): their
        ///     charts per level, and the peers' prevalence points per level as a share of the type.
        /// </summary>
        private static PeerCompare CompareWith(PeerPoolSummary summary, IReadOnlyCollection<Guid> myPool,
            IReadOnlyDictionary<Guid, Chart> charts)
        {
            var totalPoints = summary.Charts.Values.Sum(c => (double)c.Points);
            var shareByLevel = summary.Charts
                .Where(kv => kv.Value.Points > 0 && charts.ContainsKey(kv.Key))
                .GroupBy(kv => (int)charts[kv.Key].Level)
                .ToDictionary(g => g.Key, g => totalPoints == 0 ? 0 : g.Sum(kv => kv.Value.Points) / totalPoints);
            return new PeerCompare(
                myPool.Where(charts.ContainsKey).GroupBy(id => (int)charts[id].Level).ToDictionary(g => g.Key, g => g.Count()),
                shareByLevel);
        }

        /// <summary>
        ///     Who the peers are, across the lit types: public accounts listed strongest first with
        ///     their level, total, competitive levels, the types they are a peer for and how many of
        ///     the viewer's fifty of each type they also hold; private accounts counted and not
        ///     named; and the viewer's own row, for the page to place among them.
        /// </summary>
        private async Task<(IReadOnlyList<PeerRosterEntry> Roster, int PrivatePeers, PeerRosterEntry? You)> Roster(
            MixEnum mix, Guid userId, IReadOnlyCollection<ChartType> lit, ProjectionSweep sweep,
            IReadOnlyDictionary<ChartType, IReadOnlyDictionary<Guid, int>> myPools, CancellationToken cancellationToken)
        {
            var peerFor = new Dictionary<Guid, HashSet<ChartType>>();
            foreach (var type in lit)
            foreach (var peer in sweep.PeerPools[type].PeerIds)
            {
                if (!peerFor.TryGetValue(peer, out var types)) peerFor[peer] = types = new HashSet<ChartType>();
                types.Add(type);
            }

            var ids = peerFor.Keys.Append(userId).Distinct().ToArray();
            var users = (await _users.GetUsers(ids, cancellationToken)).ToDictionary(u => u.Id);
            var stats = (await _stats.GetStats(mix, ids, cancellationToken)).ToDictionary(s => s.UserId);

            PeerRosterEntry Row(Guid id, User user)
            {
                var stat = stats.GetValueOrDefault(id);
                var total = stat?.SkillRating ?? 0;
                var overlap = new Dictionary<ChartType, int>();
                if (peerFor.TryGetValue(id, out var types))
                    foreach (var type in types)
                        overlap[type] = sweep.PeerPools[type].Pools.TryGetValue(id, out var held)
                            ? held.Count(myPools[type].ContainsKey)
                            : 0;
                // The gem is read off the total on the one mix that has a ladder to read it from.
                return new PeerRosterEntry(user, total,
                    mix == MixEnum.Phoenix2 ? Phoenix2PumbilityLevel.From(total).Index : null,
                    stat?.SinglesCompetitiveLevel ?? 0, stat?.DoublesCompetitiveLevel ?? 0,
                    types ?? new HashSet<ChartType>(), overlap);
            }

            var roster = peerFor.Keys
                .Where(id => users.TryGetValue(id, out var u) && u.IsPublic)
                .Select(id => Row(id, users[id]))
                .OrderByDescending(r => r.Total)
                .ToArray();
            var privatePeers = peerFor.Keys.Count(id => !users.TryGetValue(id, out var u) || !u.IsPublic);
            var you = users.TryGetValue(userId, out var me) ? Row(userId, me) : null;
            return (roster, privatePeers, you);
        }

        /// <summary>
        ///     What players around this one score on the charts in range — the expensive half,
        ///     and the only half worth keeping. Deliberately pool-free: the pool changes which
        ///     bar an estimate is measured against, never the estimate, so all three selector
        ///     positions share one sweep instead of paying for three.
        /// </summary>
        private async Task<ProjectionSweep> Estimate(Guid userId, MixEnum mix)
        {
            var charts = (await _mediator.Send(new GetChartsQuery(mix), CancellationToken.None))
                .ToDictionary(c => c.Id);
            var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

            // The most permissive bar any pool could set, because this set has to serve all
            // three. A merged top fifty is drawn from a superset of either single type's, so
            // it never sits below both — the lower of the two per-type bars is the floor.
            var floor = Math.Min(
                (await BuildPool(ChartType.Single, userId, mix, charts, scoring, CancellationToken.None)).Baseline,
                (await BuildPool(ChartType.Double, userId, mix, charts, scoring, CancellationToken.None)).Baseline);

            // Phoenix 1 seats nobody on a rung ladder and discards this, so it does not pay for
            // the read: a third GetTop50ForPlayerQuery per sweep for a value nothing looks at.
            var finish = mix == MixEnum.Phoenix2
                ? FinishedTotal(await BuildPool(null, userId, mix, charts, scoring, CancellationToken.None))
                : null;

            // Phoenix 1 scopes its window on scoring levels; Phoenix 2 has no window and does
            // not read them.
            var scoringLevels = mix == MixEnum.Phoenix2
                ? new Dictionary<Guid, double>()
                : await _mediator.Send(new GetChartScoringLevelsQuery(mix), CancellationToken.None);

            var expectedScore = new Dictionary<Guid, PhoenixScore>();
            var spreads = new Dictionary<Guid, PeerSpread>();
            var peers = new Dictionary<ChartType, PeerGroup>();
            var pools = new Dictionary<ChartType, PeerPoolSummary>();
            var scope = new ProjectionScope(mix, charts, scoringLevels, scoring, floor,
                finish?.Total, finish?.IsEstimate ?? false);

            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
                await ProjectType(chartType, userId, scope, expectedScore, spreads, peers, pools, CancellationToken.None);

            return new ProjectionSweep(expectedScore, peers, spreads, pools);
        }

        /// <summary>
        ///     What those estimates are worth to this player, in this pool, right now. Cheap —
        ///     their own top hundred and one tier-list read — and never cached, because the bar
        ///     it measures against moves every time they play.
        /// </summary>
        private async Task<PumbilityProjection> Price(ProjectionSweep sweep,
            ProjectPumbilityGainsQuery request, CancellationToken cancellationToken)
        {
            var mix = request.Mix;
            var charts = (await _mediator.Send(new GetChartsQuery(mix), cancellationToken))
                .ToDictionary(c => c.Id);
            var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

            // One mixed pool shared by both chart types: a single gain baseline that a chart
            // of either type can displace, matching how the game aggregates.
            var pool = await BuildPool(request.ChartType, request.UserId, mix, charts, scoring, cancellationToken);

            // The pool scopes the LIST, where the estimates are deliberately type-blind.
            var expectedScore = request.ChartType is { } only
                ? sweep.ExpectedScores.Where(kv => charts.TryGetValue(kv.Key, out var c) && c.Type == only)
                    .ToDictionary(kv => kv.Key, kv => kv.Value)
                : sweep.ExpectedScores.Where(kv => charts.ContainsKey(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

            var chartDifficulty = (await _mediator.Send(new GetTierListQuery("Pass Count", mix), cancellationToken))
                .ToDictionary(s => s.ChartId, e => e.Category);

            var projectedGains = new Dictionary<Guid, double>();
            foreach (var kv in expectedScore)
            {
                var chart = charts[kv.Key];
                // Plate rides the projected score through the empirical curve — a flat EG
                // assumption overpriced plate bonuses under Phoenix 2's additive formula.
                var expectedPumbility = scoring.GetScore(chart, kv.Value,
                    ScoringConfiguration.ExpectedPlateForScore(kv.Value), false);

                // What this chart would displace. A chart already IN the pool displaces its own
                // old value; one outside displaces the 50th. Taking the current rating alone is
                // wrong for anything ranked 51-100 — that value is BELOW the bar, so the gain
                // comes out inflated by the difference. Max is exactly the rule, because being
                // in the pool means the rating is already at or above the baseline.
                var floor = pool.Ratings.TryGetValue(kv.Key, out var current)
                    ? Math.Max(current, pool.Baseline)
                    : pool.Baseline;
                var gain = expectedPumbility - floor;
                if (gain <= 0) continue;
                projectedGains[kv.Key] = gain;
            }

            // Ranked advice, not an inventory. A full window clears the bar on well over a
            // thousand charts, and nobody plans past the first hundred.
            //
            // Flat, not per chart type: the request itself carries the type now (the page's
            // pool selector scopes the whole query), so a per-type split would be counting
            // groups that only ever have one member.
            var ranked = projectedGains
                .OrderByDescending(kv => kv.Value)
                .Take(MaxTargets)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return new PumbilityProjection(
                expectedScore.Where(kv => ranked.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value),
                ranked,
                chartDifficulty,
                sweep.Peers,
                sweep.Spreads.Where(kv => ranked.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        /// <summary>
        ///     Where this player's PUMBILITY ends up if they keep the average they are holding
        ///     now, and whether that is a guess: their real total once the merged pool holds fifty,
        ///     and the pool's average out to fifty slots while it does not (D48).
        ///     <para>
        ///         The merged pool, not a per-type one, because the rung ladder is read off the
        ///         merged top fifty — the number the game's own badge is drawn from. Which also
        ///         means a full merged pool is a SETTLED number even while the type being viewed
        ///         holds twenty-odd charts, and the flag says so: that player was placed by a real
        ///         total, and no surface may tell them their peers came from an estimate.
        ///     </para>
        ///     <para>
        ///         An answer is returned all the way down to a single chart, even though nothing
        ///         under the projection gate can light up. The number is unused there — every type
        ///         is dark — but supplying it is what makes the gate twenty, so the dark chip counts
        ///         toward the threshold that will actually light this player up rather than toward
        ///         a fifty they never have to reach.
        ///     </para>
        /// </summary>
        private static (double Total, bool IsEstimate)? FinishedTotal(PoolState merged)
        {
            var ranked = merged.Ratings.Values.OrderByDescending(v => v).ToArray();
            if (ranked.Length >= PumbilityPeerPools.PoolSize)
                return (ranked.Take(PumbilityPeerPools.PoolSize).Sum(), false);
            // Nothing at all is not an estimate of anything, and answering zero would seat a
            // player the stats know the rung of at the bottom of the ladder. Null hands the
            // placement back to their standing total, which is what it was before.
            if (ranked.Length == 0) return null;
            return (ranked.Take(PeerGroup.PumbilityProjectionGate).Average() * PumbilityPeerPools.PoolSize, true);
        }

        /// <summary>What a projection run reads: the same for every chart type in the run.</summary>
        private sealed record ProjectionScope(MixEnum Mix, IReadOnlyDictionary<Guid, Chart> Charts,
            IDictionary<Guid, double> ScoringLevels, ScoringConfiguration Scoring, double Baseline,
            double? FinishedTotal, bool FinishIsEstimate);

        private async Task ProjectType(ChartType chartType, Guid userId, ProjectionScope scope,
            IDictionary<Guid, PhoenixScore> into, IDictionary<Guid, PeerSpread> spreads,
            IDictionary<ChartType, PeerGroup> peers, IDictionary<ChartType, PeerPoolSummary> pools,
            CancellationToken cancellationToken)
        {
            var (mix, charts, scoringLevels, scoring, baseline, finishedTotal, finishIsEstimate) = scope;

            var candidates = charts.Values.Where(c => c.Type == chartType);
            if (mix != MixEnum.Phoenix2)
            {
                // The same level the projector draws peers around, so the charts asked about and
                // the players asked cannot end up centred on different numbers. Phoenix 2 skips
                // this: no level window (D24) — every chart of the type is a candidate, and the
                // five-peer floor inside the projector decides which ones get a number.
                var myLevel = await _projector.CompetitiveLevel(mix, chartType, userId, cancellationToken);
                // Competitive level 1 is the no-data floor; below 10 the pool contributes nothing
                // to PUMBILITY anyway, so there is no projection worth making.
                if (myLevel <= 1) return;
                candidates = candidates
                    .Where(c => Math.Abs(ScoringLevelOf(c, scoringLevels) - myLevel) <= ScoringLevelWindow);
            }

            var scoped = candidates
                // A chart whose value at a PERFECT game still sits under the bar can never pay,
                // so nothing downstream would keep it. Dropping it here costs nothing and is
                // exact — but it is the difference between asking the database for every peer's
                // scores on every chart and asking for the ones that could matter.
                .Where(c => scoring.GetScore(c, PhoenixScore.Max, PhoenixPlate.PerfectGame, false) > baseline)
                .Select(c => new ProjectionTarget(c.Id, (int)c.Level))
                .ToArray();
            if (scoped.Length == 0) return;

            // ±1.0 on Phoenix 1, measured optimal for predicting the score itself — this page
            // quotes the number, so its accuracy is what matters rather than the ranking.
            // Phoenix 2 ignores the window; its peers are the PUMBILITY band. Both are handed the
            // catalog, so the same read also yields what the peers' pools are made of (D43).
            //
            // This is the caller that asks for the thin-band fallback (D47): what the page and
            // the home widget do with this is suggest charts to play, and one peer's score is a
            // worse suggestion than five but a better one than an empty board. The tier list's
            // own call deliberately does not ask.
            var projected = await _projector.Project(
                new ScoreProjectionRequest(mix, chartType, userId, scoped, PeerEstimator.CompetitiveWindow, charts,
                    RelaxFloorWhenEmpty: true, ProjectedTotal: finishedTotal,
                    ProjectedTotalIsEstimate: finishIsEstimate),
                cancellationToken);

            foreach (var (chartId, score) in projected.Scores) into[chartId] = score;
            if (projected.Spreads != null)
                foreach (var (chartId, spread) in projected.Spreads) spreads[chartId] = spread;
            if (projected.Group is { } group) peers[chartType] = group;
            if (projected.PeerPools is { } summary) pools[chartType] = summary;
        }

        private static double ScoringLevelOf(Chart chart, IDictionary<Guid, double> scoringLevels)
        {
            return scoringLevels.TryGetValue(chart.Id, out var level) ? level : (int)chart.Level;
        }

        private async Task<PoolState> BuildPool(ChartType? chartType, Guid userId, MixEnum mix,
            IReadOnlyDictionary<Guid, Chart> charts, ScoringConfiguration scoring,
            CancellationToken cancellationToken)
        {
            var topScores = (await _mediator.Send(
                    new GetTop50ForPlayerQuery(userId, chartType, 100, mix), cancellationToken))
                .ToDictionary(s => s.ChartId);
            var ratings = topScores.ToDictionary(kv => kv.Key,
                kv => scoring.GetScore(charts[kv.Key], kv.Value.Score!.Value,
                    kv.Value.Plate ?? PhoenixPlate.RoughGame, kv.Value.IsBroken));
            var top50 = ratings.OrderByDescending(kv => kv.Value).Take(50).ToArray();
            // A pool that isn't full displaces nothing — a new chart contributes whole. This
            // matters most at a mix launch, when nobody has fifty scores yet.
            var baseline = ratings.Count >= 50 ? top50.Min(kv => kv.Value) : 0;
            return new PoolState(ratings, baseline);
        }

        private sealed record PoolState(IReadOnlyDictionary<Guid, double> Ratings, double Baseline);
    }
}
