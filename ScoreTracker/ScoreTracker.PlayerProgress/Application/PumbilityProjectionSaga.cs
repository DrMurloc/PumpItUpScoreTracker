using ScoreTracker.Domain.Services;
using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.PlayerProgress.Contracts.Queries;

namespace ScoreTracker.PlayerProgress.Application
{
    internal sealed class PumbilityProjectionSaga : IRequestHandler<ProjectPumbilityGainsQuery, PumbilityProjection>
    {
        private readonly IMediator _mediator;
        private readonly IPlayerStatsReader _stats;
        private readonly IScoreReader _scores;

        public PumbilityProjectionSaga(IMediator mediator, IPlayerStatsReader stats,
            IScoreReader scores)
        {
            _mediator = mediator;
            _stats = stats;
            _scores = scores;
        }

        public async Task<PumbilityProjection> Handle(ProjectPumbilityGainsQuery request,
            CancellationToken cancellationToken)
        {
            var mix = request.Mix;
            var charts = (await _mediator.Send(new GetChartsQuery(mix), cancellationToken))
                .ToDictionary(c => c.Id);
            var allScores = (await _scores.GetBestScores(mix, request.UserId, cancellationToken))
                .Where(r => r.Score != null)
                .ToDictionary(s => s.ChartId);
            var scoring = ScoringConfiguration.PumbilityScoring(mix, false);

            // One mixed top-50 pool shared by both chart types: a single gain baseline
            // (the pool's lowest rating) that a chart of either type can displace.
            var mixed = await BuildPool(null, request.UserId, mix, charts, scoring, cancellationToken);
            var pools = new Dictionary<ChartType, PoolState>
            {
                [ChartType.Single] = mixed,
                [ChartType.Double] = mixed
            };

            var pooledTop50 = pools.Values.Distinct()
                .SelectMany(p => p.Top50)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            var tierList = TierListProcessor.ProcessIntoTierList("PUMBILITY", pooledTop50)
                .Where(e => e.Category != TierListCategory.Unrecorded)
                .GroupBy(e => e.Category)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ChartId).ToArray());

            var levelRange = tierList.SelectMany(t => t.Value)
                .Select(id => (int)charts[id].Level)
                .Distinct()
                .ToArray();
            if (!levelRange.Any())
                return new PumbilityProjection(
                    new Dictionary<Guid, PhoenixScore>(),
                    new Dictionary<Guid, int>(),
                    new Dictionary<(ChartType ChartType, DifficultyLevel Level), int>(),
                    new Dictionary<Guid, TierListCategory>(),
                    new Dictionary<Guid, IReadOnlyList<SkillAdjustmentRecord>>());

            var lowestLevel = DifficultyLevel.From(levelRange.Min());
            var highestLevel = DifficultyLevel.From(levelRange.Max());
            var stats = await _stats.GetStats(mix, request.UserId, cancellationToken);
            var singlesLevel = stats.SinglesCompetitiveLevel <= 10 ? 10.0 : stats.SinglesCompetitiveLevel;
            var doublesLevel = stats.DoublesCompetitiveLevel <= 10 ? 10.0 : stats.DoublesCompetitiveLevel;

            var singlesPlayers = (await _stats.GetPlayersByCompetitiveRange(mix, ChartType.Single, singlesLevel, 1,
                cancellationToken)).ToArray();
            var doublesPlayers = (await _stats.GetPlayersByCompetitiveRange(mix, ChartType.Double, doublesLevel, 1,
                cancellationToken)).ToArray();

            var chartDifficulty = (await _mediator.Send(new GetTierListQuery("Pass Count", mix), cancellationToken))
                .ToDictionary(s => s.ChartId, e => e.Category);

            var expectedScore = new Dictionary<Guid, PhoenixScore>();
            var insufficientData = new Dictionary<(ChartType ChartType, DifficultyLevel Level), int>();

            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            {
                var cohort = chartType == ChartType.Single ? singlesPlayers : doublesPlayers;
                var playerScores = (await _scores.GetScores(mix, cohort, chartType, lowestLevel,
                        highestLevel, cancellationToken))
                    .Where(s => s is { IsBroken: false, Score: not null })
                    .GroupBy(r => charts[r.ChartId].Level)
                    .ToDictionary(g => g.Key, g => g.ToArray());

                foreach (var levelGroup in playerScores)
                {
                    var chartGroup = levelGroup.Value.GroupBy(s => s.ChartId)
                        .ToDictionary(g => g.Key, g => g.Select(c => c.Score!.Value)
                            .OrderByDescending(c => c).ToArray());

                    var percentile = chartGroup.Any(c => allScores.TryGetValue(c.Key, out var score)
                                                          && score is { IsBroken: false, Score: not null })
                        ? chartGroup.Where(c => allScores.TryGetValue(c.Key, out var score)
                                                && score is { IsBroken: false, Score: not null })
                            .Average(c => Array.IndexOf(c.Value, allScores[c.Key].Score!.Value)
                                          / (double)c.Value.Count()) * .95
                        : .5;

                    var chartAverages = chartGroup
                        .Where(kv => kv.Value.Count() > 3)
                        .ToDictionary(g => g.Key, g => g.Value.Average(c => (int)c));

                    var myScores = allScores
                        .Where(kv => kv.Value.Score != null && chartAverages.ContainsKey(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value.Score!.Value);

                    if (myScores.Count() < (int)Math.Floor(.2 * chartAverages.Count()))
                    {
                        var diff = scoring.GetScore(chartType, levelGroup.Key, PhoenixLetterGrade.AA.GetMinimumScore())
                                   - pools[chartType].Baseline;
                        if (myScores.Any() && diff > 0)
                            insufficientData[(chartType, levelGroup.Key)] = (int)diff;
                        continue;
                    }

                    foreach (var chartAverage in chartAverages)
                    {
                        var target = percentile * chartGroup[chartAverage.Key].Count();
                        var highIndex = Math.Floor(target);
                        var lowIndex = Math.Ceiling(target);
                        if (lowIndex > chartGroup[chartAverage.Key].Count() - 1)
                            lowIndex = chartGroup[chartAverage.Key].Count() - 1;
                        if (highIndex < 0)
                            highIndex = 0;
                        var highScore = chartGroup[chartAverage.Key][(int)highIndex];
                        var lowScore = chartGroup[chartAverage.Key][(int)lowIndex];

                        var estimated = lowScore + (highScore - lowScore) * (lowIndex - target);
                        expectedScore[chartAverage.Key] = (int)estimated;
                    }
                }
            }

            var skillAdjustments = await ApplySkillAdjustments(expectedScore, charts, request.UserId, mix,
                singlesLevel, doublesLevel, cancellationToken);

            var projectedGains = new Dictionary<Guid, int>();
            foreach (var kv in expectedScore)
            {
                var chart = charts[kv.Key];
                var pool = pools[chart.Type];
                // Plate rides the projected score through the empirical curve — a flat
                // EG assumption overpriced plate bonuses everywhere under Phoenix 2's
                // additive formula.
                var expectedPumbility = scoring.GetScore(chart, kv.Value,
                    ScoringConfiguration.ExpectedPlateForScore(kv.Value), false);
                var expectedGains = expectedPumbility - pool.Baseline;
                if (expectedGains <= 0) continue;
                if (pool.Ratings.TryGetValue(kv.Key, out var rating))
                {
                    expectedGains = expectedPumbility - rating;
                    if (expectedGains <= 0) continue;
                }

                projectedGains[kv.Key] = (int)expectedGains;
            }

            return new PumbilityProjection(expectedScore, projectedGains, insufficientData, chartDifficulty,
                skillAdjustments);
        }

        // Half-voice on purpose: cohort interpolation is the primary signal; the skill
        // profile nudges it toward charts whose demands match the player's strengths.
        private const double SkillDamping = 0.5;

        /// <summary>
        ///     Skill-weights each expected score (docs/design/home-page-widgets.md §5):
        ///     a chart's banked skill chips pull its projection toward the player's
        ///     per-skill deviations (GetPlayerSkillDeviationsQuery — the tier-list
        ///     competence machinery). Mutates <paramref name="expectedScore" /> and
        ///     returns the per-chart "why" so targets can explain themselves.
        /// </summary>
        private async Task<Dictionary<Guid, IReadOnlyList<SkillAdjustmentRecord>>> ApplySkillAdjustments(
            Dictionary<Guid, PhoenixScore> expectedScore, IReadOnlyDictionary<Guid, Chart> charts, Guid userId,
            MixEnum mix, double singlesLevel, double doublesLevel, CancellationToken cancellationToken)
        {
            var adjustments = new Dictionary<Guid, IReadOnlyList<SkillAdjustmentRecord>>();
            if (!expectedScore.Any()) return adjustments;

            var profiles = new Dictionary<ChartType, PlayerSkillDeviations>();
            foreach (var (type, level) in new[]
                         { (ChartType.Single, singlesLevel), (ChartType.Double, doublesLevel) })
            {
                if (expectedScore.Keys.All(id => charts[id].Type != type)) continue;
                var anchor = DifficultyLevel.From(Math.Clamp((int)Math.Round(level), 1, DifficultyLevel.Max));
                var profile = await _mediator.Send(
                    new GetPlayerSkillDeviationsQuery(userId, type, anchor, mix), cancellationToken);
                if (profile.Usable) profiles[type] = profile;
            }

            if (!profiles.Any()) return adjustments;

            var chips = await _mediator.Send(new GetChartSkillChipsQuery(expectedScore.Keys.ToArray()),
                cancellationToken);

            foreach (var chartId in expectedScore.Keys.ToArray())
            {
                var raw = (int)expectedScore[chartId];
                // Deviations are measured on the floored band — below it they say nothing.
                if (raw < PlayerSkillDeviations.BandFloor) continue;
                if (!profiles.TryGetValue(charts[chartId].Type, out var profile)) continue;
                // Charts without banked step analysis get no adjustment, same as the tier list.
                if (!chips.TryGetValue(chartId, out var chartChips) || chartChips.Count == 0) continue;

                var weighted = chartChips
                    .Where(c => profile.Skills.TryGetValue(c.Skill, out var deviation) && deviation.Usable)
                    .ToArray();
                if (!weighted.Any()) continue;

                var totalWeight = weighted.Sum(c => c.Weight);
                var perSkill = weighted
                    .Select(c => new SkillAdjustmentRecord(c.Skill,
                        SkillDamping * c.Weight * profile.Skills[c.Skill].ScoreDeviation / totalWeight))
                    .Where(a => Math.Abs(a.ScoreDelta) >= 1)
                    .OrderByDescending(a => Math.Abs(a.ScoreDelta))
                    .ToArray();
                if (!perSkill.Any()) continue;

                var adjusted = Math.Clamp(raw + (int)perSkill.Sum(a => a.ScoreDelta),
                    PlayerSkillDeviations.BandFloor, PlayerSkillDeviations.BandCeiling);
                if (adjusted == raw) continue;
                expectedScore[chartId] = adjusted;
                adjustments[chartId] = perSkill;
            }

            return adjustments;
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
            var top50 = ratings.OrderByDescending(kv => kv.Value)
                .Take(50)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            // A pool that isn't full displaces nothing — a new chart contributes whole.
            // This matters most at a mix launch, when nobody has fifty scores yet.
            var baseline = ratings.Count >= 50 ? top50.Values.Min() : 0;
            return new PoolState(ratings, top50, baseline);
        }

        private sealed record PoolState(IReadOnlyDictionary<Guid, int> Ratings,
            IReadOnlyDictionary<Guid, int> Top50, int Baseline);
    }
}
