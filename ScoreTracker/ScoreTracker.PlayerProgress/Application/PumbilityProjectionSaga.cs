using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application
{
    /// <summary>
    ///     Projects what a player would score on charts they have not played, and what that
    ///     would add to their PUMBILITY (docs/design/pumbility-overhaul.md §4.1).
    ///     <para>
    ///         The arithmetic lives in <see cref="CohortEstimator" />, which is pure, so the
    ///         exploration harness measures the same code that ships. This class is the
    ///         plumbing: pick the peers, resolve what level each held when they set a score,
    ///         and price the result against the player's own top-50 bar.
    ///     </para>
    /// </summary>
    internal sealed class PumbilityProjectionSaga : IRequestHandler<ProjectPumbilityGainsQuery, PumbilityProjection>
    {
        /// <summary>
        ///     How far a chart's scoring level may sit from the player's competitive level and
        ///     still be worth projecting. Beyond this the estimate is arithmetically fine and
        ///     practically useless — nobody grinding 21s needs a number for a 26.
        /// </summary>
        private const double ScoringLevelWindow = 2.0;

        /// <summary>How many peer-estimated suggestions survive to the merge (owner, 2026-08-06).</summary>
        private const int MaxTargets = 100;

        /// <summary>
        ///     Levels of slack when reading the reference mix, which rerated these charts — a
        ///     chart sitting at 21 here may sit at 22 or 20 there.
        /// </summary>
        private const int ReferenceLevelSlack = 2;

        private readonly PumbilityProjectionCache _cache;
        private readonly IPlayerHistoryRepository _history;
        private readonly IMediator _mediator;
        private readonly IScoreReader _scores;
        private readonly IPlayerStatsReader _stats;

        public PumbilityProjectionSaga(IMediator mediator, IPlayerStatsReader stats, IScoreReader scores,
            IPlayerHistoryRepository history, PumbilityProjectionCache cache)
        {
            _mediator = mediator;
            _stats = stats;
            _scores = scores;
            _history = history;
            _cache = cache;
        }

        public async Task<PumbilityProjection> Handle(ProjectPumbilityGainsQuery request,
            CancellationToken cancellationToken)
        {
            // Two halves with nothing in common. The estimates are the cohort sweep — sized by
            // the player population, the same for all three pools, and unchanged by anything
            // the viewer does, since a player's own scores never enter their own cohort. The
            // pricing is arithmetic over their top hundred, and moves the moment they play.
            // So one is held for a day and the other is redone on every visit.
            var estimates = await _cache.GetOrAdd(request.UserId, request.Mix,
                () => Estimate(request.UserId, request.Mix));
            return await Price(estimates, request, cancellationToken);
        }

        /// <summary>
        ///     What players around this one score on the charts in range — the expensive half,
        ///     and the only half worth keeping. Deliberately pool-free: the pool changes which
        ///     bar an estimate is measured against, never the estimate, so all three selector
        ///     positions share one sweep instead of paying for three.
        /// </summary>
        private async Task<IReadOnlyDictionary<Guid, PhoenixScore>> Estimate(Guid userId, MixEnum mix)
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

            var myStats = await _stats.GetStats(mix, userId, CancellationToken.None);
            var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(mix), CancellationToken.None);

            var expectedScore = new Dictionary<Guid, PhoenixScore>();
            var scope = new ProjectionScope(mix, charts, scoringLevels, myStats, scoring, floor);

            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
                await ProjectType(chartType, userId, scope, expectedScore, CancellationToken.None);

            return expectedScore;
        }

        /// <summary>
        ///     What those estimates are worth to this player, in this pool, right now. Cheap —
        ///     their own top hundred and one tier-list read — and never cached, because the bar
        ///     it measures against moves every time they play.
        /// </summary>
        private async Task<PumbilityProjection> Price(IReadOnlyDictionary<Guid, PhoenixScore> estimates,
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
                ? estimates.Where(kv => charts.TryGetValue(kv.Key, out var c) && c.Type == only)
                    .ToDictionary(kv => kv.Key, kv => kv.Value)
                : estimates.Where(kv => charts.ContainsKey(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

            var chartDifficulty = (await _mediator.Send(new GetTierListQuery("Pass Count", mix), cancellationToken))
                .ToDictionary(s => s.ChartId, e => e.Category);

            var projectedGains = new Dictionary<Guid, int>();
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
                projectedGains[kv.Key] = (int)gain;
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
                chartDifficulty);
        }

        /// <summary>What a projection run reads: the same for every chart type in the run.</summary>
        private sealed record ProjectionScope(MixEnum Mix, IReadOnlyDictionary<Guid, Chart> Charts,
            IDictionary<Guid, double> ScoringLevels, PlayerStatsRecord MyStats,
            ScoringConfiguration Scoring, int Baseline);

        private async Task ProjectType(ChartType chartType, Guid userId, ProjectionScope scope,
            IDictionary<Guid, PhoenixScore> into, CancellationToken cancellationToken)
        {
            var (mix, charts, scoringLevels, myStats, scoring, baseline) = scope;

            // The player is measured in the mix they are looking at: their pool, their bar and
            // their level all come from this mix and nowhere else. The reference mix is a
            // detail of the PEER side only (see BestAcrossMixes).
            var reference = ReferenceMixFor(mix);

            var myLevel = CompetitiveLevelFor(myStats, chartType);
            if (myLevel <= 1 && reference != mix)
                // A launch-mix account with no scores yet has no level to match peers on. The
                // other mix names one rather than the page projecting nothing at all.
                myLevel = CompetitiveLevelFor(
                    await _stats.GetStats(reference, userId, cancellationToken), chartType);
            // Competitive level 1 is the no-data floor; below 10 the pool contributes nothing
            // to PUMBILITY anyway, so there is no projection worth making.
            if (myLevel <= 1) return;

            var scoped = charts.Values
                .Where(c => c.Type == chartType)
                .Where(c => Math.Abs(ScoringLevelOf(c, scoringLevels) - myLevel) <= ScoringLevelWindow)
                // A chart whose value at a PERFECT game still sits under the bar can never pay,
                // so nothing downstream would keep it. Dropping it here costs nothing and is
                // exact — but it is the difference between asking the database for every peer's
                // scores on ~600 charts and asking for the couple hundred that could matter.
                .Where(c => scoring.GetScore(c, PhoenixScore.Max, PhoenixPlate.PerfectGame, false) > baseline)
                .Select(c => c.Id)
                .ToArray();
            if (scoped.Length == 0) return;

            var cohort = (await _stats.GetPlayersByCompetitiveRange(mix, chartType, myLevel,
                CohortEstimator.CompetitiveWindow, cancellationToken)).ToHashSet();
            if (reference != mix)
                cohort.UnionWith(await _stats.GetPlayersByCompetitiveRange(reference, chartType, myLevel,
                    CohortEstimator.CompetitiveWindow, cancellationToken));
            cohort.Remove(userId);
            if (cohort.Count == 0) return;

            var peerScores = await BestAcrossMixes(mix, reference, cohort, scoped, charts, chartType,
                cancellationToken);

            // Their level NOW, and their level history, so a score can be dated against the
            // player they were when they set it. Both come from the reference mix, which is
            // where the level series actually runs; a peer the reference mix has never seen
            // falls back to this one.
            //
            // History is read for the peers who ACTUALLY turned up in the sweep, not for the
            // whole cohort — a cohort is several hundred players and each carries a full
            // timeline, so the ones who never played a chart in the window were pure freight.
            var voices = peerScores.Select(s => s.UserId).Distinct().ToArray();
            if (voices.Length == 0) return;

            var levelNow = (await _stats.GetStats(reference, voices, cancellationToken))
                .ToDictionary(s => s.UserId, s => CompetitiveLevelFor(s, chartType));
            if (reference != mix)
                foreach (var stats in await _stats.GetStats(mix, voices, cancellationToken))
                    if (!levelNow.ContainsKey(stats.UserId))
                        levelNow[stats.UserId] = CompetitiveLevelFor(stats, chartType);

            var history = (await _history.GetHistory(reference, voices, cancellationToken))
                .GroupBy(h => h.UserId)
                .ToDictionary(g => g.Key, g => g.OrderBy(h => h.Date).ToArray());

            foreach (var group in peerScores.GroupBy(s => s.ChartId))
            {
                var peers = group
                    .Where(s => levelNow.ContainsKey(s.UserId))
                    .Select(s => new PeerScore((int)s.Score, levelNow[s.UserId],
                        LevelWhenSet(history, s.UserId, s.RecordedAt, chartType, levelNow[s.UserId])))
                    .ToArray();

                var estimate = CohortEstimator.Estimate(peers);
                if (estimate == null) continue;

                into[group.Key] = estimate.Value;
            }
        }

        /// <summary>
        ///     The mix a peer's evidence may also be read from. Phoenix 2 rerated Phoenix 1's
        ///     charts rather than restepping them, and the score scale did not move with them:
        ///     across 2,241 player-chart pairs scored in both mixes the median difference is
        ///     zero, with 976 higher in Phoenix 2 against 994 lower. A changed scoring formula
        ///     would show a consistent offset — that spread is practice, so a peer's Phoenix 1
        ///     score is real evidence of what they can do on the same steps today.
        ///     <para>
        ///         This matters most at a launch, which is exactly when the cohort is thinnest:
        ///         Phoenix 2 has scores from tens of players where Phoenix 1 has thousands.
        ///     </para>
        /// </summary>
        private static MixEnum ReferenceMixFor(MixEnum mix)
        {
            return mix == MixEnum.Phoenix2 ? MixEnum.Phoenix : mix;
        }

        /// <summary>
        ///     Each peer's best attempt per chart across the projected mix and its reference
        ///     mix. Only the peers pool — the player's own scores are never read from another
        ///     mix, because what they are being shown is what they have done HERE.
        /// </summary>
        private async Task<IReadOnlyCollection<UserPhoenixScore>> BestAcrossMixes(MixEnum mix, MixEnum reference,
            IReadOnlyCollection<Guid> cohort, IReadOnlyCollection<Guid> chartIds,
            IReadOnlyDictionary<Guid, Chart> charts, ChartType chartType, CancellationToken cancellationToken)
        {
            var wanted = chartIds.ToHashSet();
            // The scoped set IS a level band, so it is asked for as one: a range scan the
            // index can serve, rather than several hundred chart GUIDs in an IN list. The
            // exact set is applied in memory below, so the result is identical either way.
            var levels = chartIds.Select(id => (int)charts[id].Level).ToArray();
            var min = levels.Min();
            var max = levels.Max();

            var scores = (await _scores.GetPlayerScoresInLevelRange(mix, cohort, chartType, min, max,
                cancellationToken)).ToList();

            if (reference != mix)
                // Widened, because the reference mix rerated these charts and a chart's level
                // there is not necessarily its level here. Over-fetching costs a few rows; the
                // wanted-set filter below makes it exact.
                scores.AddRange(await _scores.GetPlayerScoresInLevelRange(reference, cohort, chartType,
                    Math.Max(1, min - ReferenceLevelSlack), Math.Min(DifficultyLevel.Max, max + ReferenceLevelSlack),
                    cancellationToken));

            return scores
                .Where(s => wanted.Contains(s.ChartId))
                .GroupBy(s => (s.UserId, s.ChartId))
                .Select(g => g.OrderByDescending(s => (int)s.Score).First())
                .ToArray();
        }

        /// <summary>
        ///     The competitive level this player held when the score landed, from their own
        ///     history. Falls back to their current level when the score predates any history —
        ///     PlayerHistory begins 2024-06-04, and a no-information default of "no growth"
        ///     under-states staleness rather than inventing it.
        /// </summary>
        private static double LevelWhenSet(IReadOnlyDictionary<Guid, PlayerRatingRecord[]> history, Guid userId,
            DateTimeOffset? recordedAt, ChartType chartType, double fallback)
        {
            if (recordedAt == null || !history.TryGetValue(userId, out var rows) || rows.Length == 0)
                return fallback;

            PlayerRatingRecord? preceding = null;
            foreach (var row in rows)
            {
                if (row.Date > recordedAt.Value) break;
                preceding = row;
            }

            var chosen = preceding ?? rows[0];
            var level = chartType == ChartType.Single ? chosen.SinglesLevel : chosen.DoublesLevel;
            return level > 1 ? level : fallback;
        }

        private static double ScoringLevelOf(Chart chart, IDictionary<Guid, double> scoringLevels)
        {
            return scoringLevels.TryGetValue(chart.Id, out var level) ? level : (int)chart.Level;
        }

        private static double CompetitiveLevelFor(PlayerStatsRecord stats, ChartType chartType)
        {
            return chartType switch
            {
                ChartType.Single => stats.SinglesCompetitiveLevel,
                ChartType.Double => stats.DoublesCompetitiveLevel,
                _ => stats.CompetitiveLevel
            };
        }

        private async Task<PoolState> BuildPool(ChartType? chartType, Guid userId, MixEnum mix,
            IReadOnlyDictionary<Guid, Chart> charts, ScoringConfiguration scoring,
            CancellationToken cancellationToken)
        {
            var topScores = (await _mediator.Send(
                    new GetTop50ForPlayerQuery(userId, chartType, 100, mix), cancellationToken))
                .ToDictionary(s => s.ChartId);
            var ratings = topScores.ToDictionary(kv => kv.Key,
                kv => (int)scoring.GetScore(charts[kv.Key], kv.Value.Score!.Value,
                    kv.Value.Plate ?? PhoenixPlate.RoughGame, kv.Value.IsBroken));
            var top50 = ratings.OrderByDescending(kv => kv.Value).Take(50).ToArray();
            // A pool that isn't full displaces nothing — a new chart contributes whole. This
            // matters most at a mix launch, when nobody has fifty scores yet.
            var baseline = ratings.Count >= 50 ? top50.Min(kv => kv.Value) : 0;
            return new PoolState(ratings, baseline);
        }

        private sealed record PoolState(IReadOnlyDictionary<Guid, int> Ratings, int Baseline);
    }
}
