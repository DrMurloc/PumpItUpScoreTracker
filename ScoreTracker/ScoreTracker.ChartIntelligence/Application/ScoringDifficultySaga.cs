using ScoreTracker.ChartIntelligence.Contracts.Messages;
using MassTransit;
using MediatR;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application
{
    internal sealed class ScoringDifficultySaga : IConsumer<RecalculateScoringDifficultyCommand>,
        IConsumer<RecalculateChartLetterDifficultiesCommand>,
        IRequestHandler<GetChartScoringLevelsQuery, IDictionary<Guid, double>>,
        IRequestHandler<GetChartLetterDifficultiesQuery,
            IReadOnlyDictionary<Guid, IReadOnlyDictionary<ParagonLevel, double>>>
    {
        /// <summary>
        ///     Every chart gets a scoring level; an unmeasured one reads as its listed level
        ///     (owner, 2026-07-15). The listed level is what the chart claims about itself, so
        ///     it is the honest prior until scores say otherwise — and a null forced every
        ///     caller to invent its own answer, which is how "what scoring level is this" ended
        ///     up meaning four different things across the app.
        ///     ~13% of charts in the competitive range have no measured level. The interpolation
        ///     that produces the real ones lands near the folder anyway (mean offset +0.21), so
        ///     the fallback is not a wild guess — but it IS a guess, and callers that draw a
        ///     distinction between "measured" and "assumed" must read the repository directly.
        /// </summary>
        public async Task<IDictionary<Guid, double>> Handle(GetChartScoringLevelsQuery request,
            CancellationToken cancellationToken)
        {
            var measured = await _scoringLevels.GetScoringLevels(request.Mix, cancellationToken);
            var charts = await _chartRepository.GetCharts(request.Mix, cancellationToken: cancellationToken);
            foreach (var chart in charts)
                if (!measured.ContainsKey(chart.Id))
                    measured[chart.Id] = chart.Level;

            return measured;
        }

        public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<ParagonLevel, double>>> Handle(
            GetChartLetterDifficultiesQuery request, CancellationToken cancellationToken)
        {
            return (await _chartRepository.GetChartLetterGradeDifficulties(request.ChartIds, cancellationToken))
                .ToDictionary(d => d.ChartId,
                    d => (IReadOnlyDictionary<ParagonLevel, double>)d.Percentiles
                        .ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        private const int LevelDiff = 3;
        private readonly IChartRepository _chartRepository;
        private readonly IScoreReader _scores;
        private readonly IPlayerStatsReader _playerStats;
        private readonly IChartScoringLevelRepository _scoringLevels;

        public ScoringDifficultySaga(IChartRepository chartRepository,
            IScoreReader scores,
            IPlayerStatsReader playerStats,
            IChartScoringLevelRepository scoringLevels)
        {
            _chartRepository = chartRepository;
            _scores = scores;
            _playerStats = playerStats;
            _scoringLevels = scoringLevels;
        }

        public async Task UpdateChartLetterLevel(MixEnum mix, ChartType chartType, DifficultyLevel level,
            CancellationToken cancellationToken = default)
        {
            var charts =
                (await _chartRepository.GetCharts(mix, level, chartType,
                    cancellationToken: cancellationToken))
                .ToArray();
            if (!charts.Any()) return;
            var scores = (await _scores.GetScores(mix, chartType, level, cancellationToken))
                .Where(s => s.Record is { IsBroken: false, Score: not null }).ToArray();
            if (!scores.Any()) return;
            var stats = (await _playerStats.GetStats(mix, scores.Select(p => p.UserId).Distinct(), cancellationToken))
                .ToDictionary(s => s.UserId);
            var results = charts.ToDictionary(c => c.Id,
                c => (IDictionary<ParagonLevel, double>)new Dictionary<ParagonLevel, double>());

            for (var letter = ParagonLevel.AA; letter <= ParagonLevel.PG; letter++)
            {
                var threshold = letter.MinThreshold(mix);
                var relevantScores = scores.Where(s => s.Record.Score != null && s.Record.Score >= threshold);


                var sums = charts.ToDictionary(c => c.Id, c => 0.0);
                foreach (var record in relevantScores)
                {
                    var competitiveLevel = chartType == ChartType.Single
                        ? stats[record.UserId].SinglesCompetitiveLevel
                        : stats[record.UserId].DoublesCompetitiveLevel;
                    if (competitiveLevel < 5)
                        continue;
                    sums[record.Record.ChartId] += Math.Pow(1.25, level + .5 - competitiveLevel);
                }

                foreach (var kv in sums) results[kv.Key][letter] = kv.Value;
            }

            var orderedLevelResults = results.SelectMany(kv => kv.Value.Select(k => k))
                .GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Select(kv => kv.Value)
                    .OrderBy(d => d).ToArray());
            var updates = new List<ChartLetterGradeDifficulty>();
            foreach (var chartKv in results)
            {
                var weight = chartKv.Value;
                var percentiles = weight.ToDictionary(levelKv => levelKv.Key, levelKv =>
                    100.0 * orderedLevelResults[levelKv.Key].Select((d, i) => (d, i))
                        .First(d => Math.Abs(d.d - levelKv.Value) < .001).i /
                    orderedLevelResults[levelKv.Key].Length);
                updates.Add(new ChartLetterGradeDifficulty(chartKv.Key, percentiles, weight));
            }

            await _chartRepository.UpdateChartLetterDifficulties(updates, cancellationToken);
        }

        public async Task Consume(ConsumeContext<RecalculateChartLetterDifficultiesCommand> context)
        {
            foreach (var level in DifficultyLevel.All)
            foreach (var chartType in new[] { ChartType.Single, ChartType.Double, ChartType.CoOp })
                await UpdateChartLetterLevel(context.Message.Mix, chartType, level, context.CancellationToken);
        }

        public async Task Consume(ConsumeContext<RecalculateScoringDifficultyCommand> context)
        {
            var cancellationToken = context.CancellationToken;
            var mix = context.Message.Mix;
            var charts = (await _chartRepository.GetCharts(mix, cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);
            foreach (var level in DifficultyLevel.All)
            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            {
                var max = level + LevelDiff;
                var min = level - LevelDiff;
                if (min < 1) min = 1;
                if (max > 26 && chartType == ChartType.Single) max = 26;
                if (max > 27 && chartType == ChartType.Double) max = 27;

                var phoenixScores = new List<(Guid UserId, RecordedPhoenixScore Record)>();
                for (var l = min; l <= max; l++)
                    phoenixScores.AddRange(
                        (await _scores.GetScores(mix, chartType, l, context.CancellationToken)).Where(s =>
                            s.Record.Score != null));
                var userIds = phoenixScores.Select(u => u.UserId).Distinct().ToArray();
                var playerWeights = new Dictionary<Guid, IDictionary<DifficultyLevel, double>>();
                foreach (var userId in userIds)
                {
                    var stats = await _playerStats.GetStats(mix, userId, CancellationToken.None);
                    var competitiveLevel = chartType == ChartType.Single
                        ? stats.SinglesCompetitiveLevel
                        : stats.DoublesCompetitiveLevel;
                    if (!playerWeights.ContainsKey(userId))
                        playerWeights[userId] = new Dictionary<DifficultyLevel, double>();
                    for (var l = min; l <= max; l++)
                        playerWeights[userId][l] = Math.Pow(.5, Math.Abs(level + .5 - competitiveLevel));
                }

                var chartScores = new Dictionary<Guid, double>();
                foreach (var scoreGroup in phoenixScores.GroupBy(s => s.Record.ChartId))
                {
                    if (charts[scoreGroup.Key].Level < 23 &&
                        scoreGroup.All(s => playerWeights[s.UserId][charts[scoreGroup.Key].Level] < .5))
                    {
                        chartScores[scoreGroup.Key] = 0;
                        continue;
                    }

                    var avg = scoreGroup.Average(g => (double)(int)g.Record.Score!.Value);

                    var stdDev = StdDev(scoreGroup.Select(g => (double)(int)g.Record.Score!.Value), false);
                    var minScore = (PhoenixScore)(int)avg - stdDev * 1.5;
                    var maxScore = (PhoenixScore)(int)avg + stdDev * 1.5;
                    var refinedGroup = scoreGroup.Where(s => s.Record.Score >= minScore && s.Record.Score <= maxScore);
                    var total = 0.0;
                    var weight = 0.0;
                    foreach (var record in refinedGroup)
                    {
                        total += (int)record.Record.Score!.Value *
                                 playerWeights[record.UserId][charts[record.Record.ChartId].Level];
                        weight += playerWeights[record.UserId][charts[record.Record.ChartId].Level];
                    }

                    chartScores[scoreGroup.Key] = total / weight;
                }

                var levelAverages = chartScores.Where(kv => kv.Value > .01).GroupBy(kv => charts[kv.Key].Level)
                    .ToDictionary(group => group.Key, group =>
                        group.Key == level ? group.Average(g => g.Value) :
                        group.Key < level ? group.Average(g => g.Value) +
                                            .5 * StdDev(group.Select(g => (double)(int)g.Value), false) :
                        group.Average(g => g.Value) - .5 * StdDev(group.Select(g => (double)(int)g.Value), false));

                var calculatedLevels = new Dictionary<Guid, double>();
                //var average = chartScores.Values.Average();
                //var standardDev = StdDev(chartScores.Values,false);
                if (!levelAverages.Any())
                {
                    foreach (var kv in chartScores.Where(c => charts[c.Key].Level == level))
                        await _scoringLevels.SaveScoringLevel(mix, kv.Key, null, context.CancellationToken);

                    continue;
                }

                min = levelAverages.Min(la => la.Key);
                max = levelAverages.Max(la => la.Key);

                var lowStandardDev =
                    StdDev(
                        chartScores.Where(kv => kv.Value > .01).Where(c => charts[c.Key].Level == min)
                            .Select(kv => kv.Value), false);

                var highStandardDev =
                    StdDev(
                        chartScores.Where(kv => kv.Value > .01).Where(c => charts[c.Key].Level == max)
                            .Select(kv => kv.Value), false);

                foreach (var kv in chartScores.Where(c => charts[c.Key].Level == level))
                {
                    if (charts[kv.Key].Song.Name == "Vanish" && charts[kv.Key].Level == 10)
                    {
                        //
                    }

                    if (kv.Value == 0)
                    {
                        calculatedLevels[kv.Key] = 0;
                        continue;
                    }

                    if (kv.Value > levelAverages[min])
                    {
                        if (lowStandardDev == 0)
                        {
                            calculatedLevels[kv.Key] = min + .5;
                            continue;
                        }

                        calculatedLevels[kv.Key] = min + .5 - (kv.Value - levelAverages[min]) / (8.0 * lowStandardDev);
                        continue;
                    }

                    if (kv.Value <= levelAverages[max])
                    {
                        if (highStandardDev == 0)
                        {
                            calculatedLevels[kv.Key] = max + .5;
                            continue;
                        }

                        calculatedLevels[kv.Key] =
                            max + .5 + (levelAverages[max] - kv.Value) / (8.0 * highStandardDev);
                        continue;
                    }

                    for (var l = min; l < max; l++)
                        if (kv.Value <= levelAverages[l] && kv.Value > levelAverages[l + 1])
                            calculatedLevels[kv.Key] =
                                l + .5 + (kv.Value - levelAverages[l]) / (levelAverages[l + 1] - levelAverages[l]);
                    //var levelAdjust =(2.0/(.5+levelDiff)) *(average - kv.Value) / (standardDev);
                    //_calculatedLevels[kv.Key] = (level + .5) + levelAdjust;
                }

                foreach (var cl in calculatedLevels)
                    await _scoringLevels.SaveScoringLevel(mix, cl.Key, cl.Value, cancellationToken);
            }
        }


        public static double StdDev(IEnumerable<double> values,
            bool asSample)
        {
            // Get the mean.
            var mean = values.Sum() / values.Count();

            // Get the sum of the squares of the differences
            // between the values and the mean.
            var squaresQuery =
                from double value in values
                select (value - mean) * (value - mean);
            var sumOfSquares = squaresQuery.Sum();

            if (asSample)
                return Math.Sqrt(sumOfSquares / (values.Count() - 1));
            return Math.Sqrt(sumOfSquares / values.Count());
        }
    }
}
