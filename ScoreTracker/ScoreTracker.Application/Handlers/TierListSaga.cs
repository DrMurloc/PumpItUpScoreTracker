using MassTransit;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class TierListSaga : IConsumer<ChartDifficultyUpdatedEvent>,
    IConsumer<ProcessScoresTiersListCommand>
{
    private readonly IChartDifficultyRatingRepository _chartRatings;
    private readonly IChartRepository _chartRepository;
    private readonly ITierListRepository _tierLists;
    private readonly IPhoenixRecordRepository _scores;

    public TierListSaga(IChartDifficultyRatingRepository chartRatings, IChartRepository chartRepository,
        ITierListRepository tierLists, IPhoenixRecordRepository scores)
    {
        _chartRatings = chartRatings;
        _chartRepository = chartRepository;
        _tierLists = tierLists;
        _scores = scores;
    }


    public async Task Consume(ConsumeContext<ChartDifficultyUpdatedEvent> context)
    {
        var cancellationToken = context.CancellationToken;
        var charts = (await _chartRepository.GetCharts(MixEnum.Phoenix, context.Message.Level,
                context.Message.ChartType, cancellationToken: cancellationToken))
            .ToArray();
        var ratings = (await _chartRatings.GetAllChartRatedDifficulties(MixEnum.Phoenix, cancellationToken))
            .ToDictionary(r => r.ChartId);
        var order = 0;
        foreach (var chart in charts)
        {
            if (!ratings.ContainsKey(chart.Id)) continue;

            var rating = ratings[chart.Id];

            var diff = rating.Difficulty - chart.Level - .5;
            switch (diff)
            {
                case <= -.75:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Overrated, order),
                        cancellationToken);
                    break;
                case <= -.375:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.VeryEasy, order),
                        cancellationToken);
                    break;
                case <= -.125:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Easy, order),
                        cancellationToken);
                    break;
                case < .125:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Medium, order),
                        cancellationToken);
                    break;
                case < .375:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Hard, order),
                        cancellationToken);
                    break;
                case < .75:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.VeryHard, order),
                        cancellationToken);
                    break;
                default:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Underrated, order),
                        cancellationToken);
                    break;
            }

            order++;
        }
    }

    public async Task Consume(ConsumeContext<ProcessScoresTiersListCommand> context)
    {
        for (var level = 1; level <= 28; level++)
            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            {
                var filtered = await _chartRepository.GetCharts(MixEnum.Phoenix, level, chartType,
                    cancellationToken: context.CancellationToken);

                var allPhoenixScores = (await _scores.GetAllPlayerScores(chartType, level, context.CancellationToken))
                    .GroupBy(r => r.userId).ToDictionary(g => g.Key, g => g.Select(r => r.record).ToArray());

                var validCharts = allPhoenixScores.Values.SelectMany(g => g.Where(r => r.Score != null))
                    .Select(r => r.ChartId).Distinct().ToHashSet();

                var filteredScoreArray = filtered.Where(c => validCharts.Contains(c.Id)).ToArray();
                if (!filteredScoreArray.Any()) continue;
                var chartCount = filteredScoreArray.ToDictionary(c => c.Id, c => 0);
                var chartTotal = filteredScoreArray.ToDictionary(c => c.Id, c => 0);

                foreach (var scores in allPhoenixScores.Values)
                {
                    var scoresDict = scores.ToDictionary(s => s.ChartId);
                    var scoreInts = scoresDict.Values.Where(s => s.Score != null).Select(s => (int)s.Score!.Value)
                        .ToArray();
                    if (scoreInts.Length < 5 || (level > 23 && scoreInts.Length < 3)) continue;
                    var standardDeviation = StdDev(scoreInts, true);
                    var average = scoreInts.Average();
                    var mediumMin = average - standardDeviation / 2;
                    var easyMin = average + standardDeviation / 2;
                    var veryEasyMin = average + standardDeviation;
                    var oneLevelOverrated = average + standardDeviation * 1.5;
                    var hardMin = average - standardDeviation;
                    var veryHardMin = average - standardDeviation * 1.5;
                    foreach (var chart in filteredScoreArray.Where(c => scoresDict.ContainsKey(c.Id)))
                    {
                        var score = (int)(scoresDict[chart.Id]?.Score ?? 0);
                        chartCount[chart.Id]++;
                        if (score < veryHardMin)
                            chartTotal[chart.Id] += 3;
                        else if (score < hardMin)
                            chartTotal[chart.Id] += 2;
                        else if (score < mediumMin)
                            chartTotal[chart.Id] += 1;
                        else if (score < easyMin)
                            chartTotal[chart.Id] += 0;
                        else if (score < veryEasyMin)
                            chartTotal[chart.Id] += -1;
                        else if (score < oneLevelOverrated)
                            chartTotal[chart.Id] += -2;
                        else
                            chartTotal[chart.Id] += -3;
                    }
                }

                var averages =
                    chartTotal.ToDictionary(kv => kv.Key, kv => chartTotal[kv.Key] / (double)chartCount[kv.Key]);
                var order = 0;
                foreach (var chart in filteredScoreArray.OrderBy(c => averages[c.Id]))
                {
                    var average = averages[chart.Id];
                    switch (average)
                    {
                        case < -2.5:
                            await _tierLists.SaveEntry(
                                new SongTierListEntry("Scores", chart.Id, TierListCategory.Overrated, order),
                                context.CancellationToken);
                            break;
                        case < -1.5:
                            await _tierLists.SaveEntry(
                                new SongTierListEntry("Scores", chart.Id, TierListCategory.VeryEasy, order),
                                context.CancellationToken);
                            break;
                        case < -.5:
                            await _tierLists.SaveEntry(
                                new SongTierListEntry("Scores", chart.Id, TierListCategory.Easy, order),
                                context.CancellationToken);
                            break;
                        case <= .5:
                            await _tierLists.SaveEntry(
                                new SongTierListEntry("Scores", chart.Id, TierListCategory.Medium, order),
                                context.CancellationToken);
                            break;
                        case <= 1.5:
                            await _tierLists.SaveEntry(
                                new SongTierListEntry("Scores", chart.Id, TierListCategory.Hard, order),
                                context.CancellationToken);
                            break;
                        case <= 2.5:
                            await _tierLists.SaveEntry(
                                new SongTierListEntry("Scores", chart.Id, TierListCategory.VeryHard, order),
                                context.CancellationToken);
                            break;
                        default:
                            await _tierLists.SaveEntry(
                                new SongTierListEntry("Scores", chart.Id, TierListCategory.Underrated, order),
                                context.CancellationToken);
                            break;
                    }

                    order++;
                }
            }
    }

    public static double StdDev(IEnumerable<int> values,
        bool as_sample)
    {
        // Get the mean.
        double mean = values.Sum() / values.Count();

        // Get the sum of the squares of the differences
        // between the values and the mean.
        var squares_query =
            from int value in values
            select (value - mean) * (value - mean);
        var sum_of_squares = squares_query.Sum();

        if (as_sample)
            return Math.Sqrt(sum_of_squares / (values.Count() - 1));
        return Math.Sqrt(sum_of_squares / values.Count());
    }
}