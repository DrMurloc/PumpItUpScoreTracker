using MassTransit;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed class ScoringDifficultySaga : IConsumer<CalculateScoringDifficultyEvent>
    {
        private const int LevelDiff = 3;
        private readonly IChartRepository _chartRepository;
        private readonly IPhoenixRecordRepository _scores;
        private readonly IPlayerStatsRepository _playerStats;

        public ScoringDifficultySaga(IChartRepository chartRepository,
            IPhoenixRecordRepository scores,
            IPlayerStatsRepository playerStats)
        {
            _chartRepository = chartRepository;
            _scores = scores;
            _playerStats = playerStats;
        }

        public async Task Consume(ConsumeContext<CalculateScoringDifficultyEvent> context)
        {
            var cancellationToken = context.CancellationToken;
            var charts = (await _chartRepository.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
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
                        (await _scores.GetAllPlayerScores(chartType, l, context.CancellationToken)).Where(s =>
                            s.record.Score != null));
                var userIds = phoenixScores.Select(u => u.UserId).Distinct().ToArray();
                var playerWeights = new Dictionary<Guid, IDictionary<DifficultyLevel, double>>();
                foreach (var userId in userIds)
                {
                    var stats = await _playerStats.GetStats(userId, CancellationToken.None);
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
                        await _chartRepository.UpdateScoreLevel(MixEnum.Phoenix, kv.Key, 0, context.CancellationToken);

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
                    await _chartRepository.UpdateScoreLevel(MixEnum.Phoenix, cl.Key, cl.Value, cancellationToken);
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
