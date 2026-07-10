using ScoreTracker.Domain.Services;
using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
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
            var topScores = (await _mediator.Send(
                    new GetTop50ForPlayerQuery(request.UserId, null, 100, mix), cancellationToken))
                .ToDictionary(s => s.ChartId);
            // TODO(P2-pumbility): projections still model ONE mixed top-50 pool. Phoenix 2
            // uses two independent pools (S+D), so its gain baseline ("lowest of top 50")
            // should be per-pool. The page is disabled on Phoenix 2; proper two-pool
            // projection lands with the What-Should-I-Play overhaul.
            var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
            var ratings = topScores.ToDictionary(kv => kv.Key,
                kv => (int)scoring.GetScore(charts[kv.Key], kv.Value.Score!.Value,
                    kv.Value.Plate ?? PhoenixPlate.RoughGame, kv.Value.IsBroken));

            var top50ForTierList = topScores.Values
                .OrderByDescending(s => ratings[s.ChartId])
                .Take(50)
                .ToDictionary(s => s.ChartId, s => ratings[s.ChartId]);
            var tierList = TierListProcessor.ProcessIntoTierList("PUMBILITY", top50ForTierList)
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
                    new Dictionary<Guid, TierListCategory>());

            var lowestLevel = DifficultyLevel.From(levelRange.Min());
            var highestLevel = DifficultyLevel.From(levelRange.Max());
            var stats = await _stats.GetStats(mix, request.UserId, cancellationToken);
            var singlesLevel = stats.SinglesCompetitiveLevel <= 10 ? 10.0 : stats.SinglesCompetitiveLevel;
            var doublesLevel = stats.DoublesCompetitiveLevel <= 10 ? 10.0 : stats.DoublesCompetitiveLevel;
            var lowestScore = ratings.OrderByDescending(kv => kv.Value).Take(50).Min(kv => kv.Value);

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
                                   - lowestScore;
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

            var projectedGains = new Dictionary<Guid, int>();
            foreach (var kv in expectedScore)
            {
                var expectedPumbility = scoring.GetScore(charts[kv.Key], kv.Value, PhoenixPlate.ExtremeGame, false);
                var expectedGains = expectedPumbility - lowestScore;
                if (expectedGains <= 0) continue;
                if (ratings.TryGetValue(kv.Key, out var rating))
                {
                    expectedGains = expectedPumbility - rating;
                    if (expectedGains <= 0) continue;
                }

                projectedGains[kv.Key] = (int)expectedGains;
            }

            return new PumbilityProjection(expectedScore, projectedGains, insufficientData, chartDifficulty);
        }
    }
}
