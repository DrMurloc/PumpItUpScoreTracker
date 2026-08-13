using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
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
    ///     would add to their PUMBILITY (docs/design/pumbility-overhaul.md §4.1).
    ///     <para>
    ///         The projection itself is <see cref="IScoreProjector" />, shared with the
    ///         personalized Score tier list so the two surfaces cannot answer "what would you
    ///         score here" differently. What is left here is this page's own half: which charts
    ///         are worth asking about, and what the answers are worth against the player's own
    ///         top-50 bar.
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

        private readonly PumbilityProjectionCache _cache;
        private readonly IMediator _mediator;
        private readonly IScoreProjector _projector;

        public PumbilityProjectionSaga(IMediator mediator, IScoreProjector projector,
            PumbilityProjectionCache cache)
        {
            _mediator = mediator;
            _projector = projector;
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

            var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(mix), CancellationToken.None);

            var expectedScore = new Dictionary<Guid, PhoenixScore>();
            var scope = new ProjectionScope(mix, charts, scoringLevels, scoring, floor);

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
                chartDifficulty);
        }

        /// <summary>What a projection run reads: the same for every chart type in the run.</summary>
        private sealed record ProjectionScope(MixEnum Mix, IReadOnlyDictionary<Guid, Chart> Charts,
            IDictionary<Guid, double> ScoringLevels, ScoringConfiguration Scoring, double Baseline);

        private async Task ProjectType(ChartType chartType, Guid userId, ProjectionScope scope,
            IDictionary<Guid, PhoenixScore> into, CancellationToken cancellationToken)
        {
            var (mix, charts, scoringLevels, scoring, baseline) = scope;

            // The same level the projector draws peers around, so the charts asked about and the
            // players asked cannot end up centred on different numbers.
            var myLevel = await _projector.CompetitiveLevel(mix, chartType, userId, cancellationToken);
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
                .Select(c => new ProjectionTarget(c.Id, (int)c.Level))
                .ToArray();
            if (scoped.Length == 0) return;

            // ±1.0, measured optimal for predicting the score itself — this page quotes the
            // number, so its accuracy is what matters rather than the ranking.
            var projected = await _projector.Project(
                new ScoreProjectionRequest(mix, chartType, userId, scoped, CohortEstimator.CompetitiveWindow),
                cancellationToken);

            foreach (var (chartId, score) in projected.Scores) into[chartId] = score;
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
