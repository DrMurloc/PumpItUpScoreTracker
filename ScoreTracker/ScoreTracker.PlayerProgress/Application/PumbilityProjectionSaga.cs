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

        private readonly IPlayerHistoryRepository _history;
        private readonly IMediator _mediator;
        private readonly IScoreReader _scores;
        private readonly IPlayerStatsReader _stats;

        public PumbilityProjectionSaga(IMediator mediator, IPlayerStatsReader stats, IScoreReader scores,
            IPlayerHistoryRepository history)
        {
            _mediator = mediator;
            _stats = stats;
            _scores = scores;
            _history = history;
        }

        public async Task<PumbilityProjection> Handle(ProjectPumbilityGainsQuery request,
            CancellationToken cancellationToken)
        {
            var mix = request.Mix;
            var charts = (await _mediator.Send(new GetChartsQuery(mix), cancellationToken))
                .ToDictionary(c => c.Id);
            var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

            // One mixed pool shared by both chart types: a single gain baseline that a chart
            // of either type can displace, matching how the game aggregates.
            var pool = await BuildPool(request.ChartType, request.UserId, mix, charts, scoring, cancellationToken);

            var myStats = await _stats.GetStats(mix, request.UserId, cancellationToken);
            var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(mix), cancellationToken);

            var expectedScore = new Dictionary<Guid, PhoenixScore>();
            var evidence = new Dictionary<Guid, ProjectionEvidence>();

            var types = request.ChartType is { } only
                ? new[] { only }
                : new[] { ChartType.Single, ChartType.Double };

            foreach (var chartType in types)
                await ProjectType(chartType, request.UserId, mix, charts, scoringLevels, myStats,
                    expectedScore, evidence, cancellationToken);

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

                var floor = pool.Ratings.TryGetValue(kv.Key, out var current) ? current : pool.Baseline;
                var gain = expectedPumbility - floor;
                if (gain <= 0) continue;
                projectedGains[kv.Key] = (int)gain;
            }

            return new PumbilityProjection(expectedScore, projectedGains, chartDifficulty, evidence);
        }

        private async Task ProjectType(ChartType chartType, Guid userId, MixEnum mix,
            IReadOnlyDictionary<Guid, Chart> charts, IDictionary<Guid, double> scoringLevels,
            PlayerStatsRecord myStats, IDictionary<Guid, PhoenixScore> expectedScore,
            IDictionary<Guid, ProjectionEvidence> evidence, CancellationToken cancellationToken)
        {
            var myLevel = CompetitiveLevelFor(myStats, chartType);
            // Competitive level 1 is the no-data floor; below 10 the pool contributes nothing
            // to PUMBILITY anyway, so there is no projection worth making.
            if (myLevel <= 1) return;

            var scoped = charts.Values
                .Where(c => c.Type == chartType)
                .Where(c => Math.Abs(ScoringLevelOf(c, scoringLevels) - myLevel) <= ScoringLevelWindow)
                .Select(c => c.Id)
                .ToArray();
            if (scoped.Length == 0) return;

            var cohort = (await _stats.GetPlayersByCompetitiveRange(mix, chartType, myLevel,
                CohortEstimator.CompetitiveWindow, cancellationToken)).ToHashSet();
            cohort.Remove(userId);
            if (cohort.Count == 0) return;

            // Their level NOW, and their whole level history, so a score can be dated against
            // the player they were when they set it. One read each for the cohort (§6.3).
            var levelNow = (await _stats.GetStats(mix, cohort, cancellationToken))
                .ToDictionary(s => s.UserId, s => CompetitiveLevelFor(s, chartType));
            var history = (await _history.GetHistory(mix, cohort, cancellationToken))
                .GroupBy(h => h.UserId)
                .ToDictionary(g => g.Key, g => g.OrderBy(h => h.Date).ToArray());

            var peerScores = await _scores.GetPlayerScores(mix, cohort, scoped, cancellationToken);

            foreach (var group in peerScores.GroupBy(s => s.ChartId))
            {
                var peers = group
                    .Where(s => levelNow.ContainsKey(s.UserId))
                    .Select(s => new PeerScore((int)s.Score, levelNow[s.UserId],
                        LevelWhenSet(history, s.UserId, s.RecordedAt, chartType, levelNow[s.UserId])))
                    .ToArray();

                var estimate = CohortEstimator.Estimate(peers);
                if (estimate == null) continue;

                expectedScore[group.Key] = estimate.Value;
                evidence[group.Key] = new ProjectionEvidence(peers.Length,
                    Math.Round(CohortEstimator.Evidence(peers), 2), Spread(peers));
            }
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

        /// <summary>Points between the 10th and 90th percentile — how much the peers disagreed.</summary>
        private static int Spread(IReadOnlyCollection<PeerScore> peers)
        {
            if (peers.Count < 2) return 0;
            var ordered = peers.Select(p => p.Score).OrderBy(s => s).ToArray();
            var low = ordered[(int)Math.Floor(0.10 * (ordered.Length - 1))];
            var high = ordered[(int)Math.Ceiling(0.90 * (ordered.Length - 1))];
            return high - low;
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
