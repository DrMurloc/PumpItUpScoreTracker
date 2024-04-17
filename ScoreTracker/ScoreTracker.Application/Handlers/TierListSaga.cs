using MassTransit;
using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers;

public sealed class TierListSaga : IConsumer<ChartDifficultyUpdatedEvent>,
    IConsumer<ProcessScoresTiersListCommand>,
    IRequestHandler<GetMyRelativeTierListQuery, IEnumerable<SongTierListEntry>>
{
    private readonly IChartDifficultyRatingRepository _chartRatings;
    private readonly IChartRepository _chartRepository;
    private readonly ITierListRepository _tierLists;
    private readonly IPhoenixRecordRepository _scores;
    private readonly ICurrentUserAccessor _currentUser;

    public TierListSaga(IChartDifficultyRatingRepository chartRatings, IChartRepository chartRepository,
        ITierListRepository tierLists, IPhoenixRecordRepository scores,
        ICurrentUserAccessor currentUser)
    {
        _chartRatings = chartRatings;
        _chartRepository = chartRepository;
        _tierLists = tierLists;
        _scores = scores;
        _currentUser = currentUser;
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
                    .Where(s => s.record.Score != null)
                    .GroupBy(r => r.userId).ToDictionary(g => g.Key.ToString(),
                        g => (IDictionary<Guid, PhoenixScore>)g.ToDictionary(p => p.userId,
                            p => p.record.Score!.Value));

                var results = ProcessIntoTierList(allPhoenixScores, level, "Scores");
                foreach (var result in results) await _tierLists.SaveEntry(result, context.CancellationToken);
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

    public static IEnumerable<SongTierListEntry> ProcessIntoTierList(
        IDictionary<string, IDictionary<Guid, PhoenixScore>> userScores, DifficultyLevel level, string listName)
    {
        var includedChartIds = userScores.Values.SelectMany(kv => kv.Select(kv2 => kv2.Key)).Distinct().ToArray();
        var chartCount = includedChartIds.ToDictionary(c => c, c => 0);
        var chartTotal = includedChartIds.ToDictionary(c => c, c => 0);

        foreach (var scores in userScores.Values)
        {
            var scoresDict = scores.ToDictionary(s => s.Key, s => s.Value);
            var scoreInts = scoresDict.Values.Select(s => (int)s)
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
            foreach (var chart in includedChartIds.Where(c => scoresDict.ContainsKey(c)))
            {
                var score = (int)scoresDict[chart];
                chartCount[chart]++;
                if (score < veryHardMin)
                    chartTotal[chart] += 3;
                else if (score < hardMin)
                    chartTotal[chart] += 2;
                else if (score < mediumMin)
                    chartTotal[chart] += 1;
                else if (score < easyMin)
                    chartTotal[chart] += 0;
                else if (score < veryEasyMin)
                    chartTotal[chart] += -1;
                else if (score < oneLevelOverrated)
                    chartTotal[chart] += -2;
                else
                    chartTotal[chart] += -3;
            }
        }

        var averages =
            chartTotal.ToDictionary(kv => kv.Key, kv => chartTotal[kv.Key] / (double)chartCount[kv.Key]);
        var order = 0;
        var result = new List<SongTierListEntry>();
        foreach (var chart in includedChartIds.OrderBy(c => averages[c]))
        {
            var average = averages[chart];
            switch (average)
            {
                case < -2.5:
                    result.Add(
                        new SongTierListEntry(listName, chart, TierListCategory.Overrated, order));
                    break;
                case < -1.5:
                    result.Add(
                        new SongTierListEntry(listName, chart, TierListCategory.VeryEasy, order));
                    break;
                case < -.5:
                    result.Add(
                        new SongTierListEntry(listName, chart, TierListCategory.Easy, order));
                    break;
                case <= .5:
                    result.Add(
                        new SongTierListEntry(listName, chart, TierListCategory.Medium, order));
                    break;
                case <= 1.5:
                    result.Add(
                        new SongTierListEntry(listName, chart, TierListCategory.Hard, order));
                    break;
                case <= 2.5:
                    result.Add(
                        new SongTierListEntry(listName, chart, TierListCategory.VeryHard, order));
                    break;
                default:
                    result.Add(
                        new SongTierListEntry(listName, chart, TierListCategory.Underrated, order));
                    break;
            }

            order++;
        }

        return result;
    }

    public async Task<IEnumerable<SongTierListEntry>> Handle(GetMyRelativeTierListQuery request,
        CancellationToken cancellationToken)
    {
        var filtered = await _chartRepository.GetCharts(MixEnum.Phoenix, request.Level, request.ChartType,
            cancellationToken: cancellationToken);
        var phoenixScores =
            (await _scores.GetRecordedScores(_currentUser.User.Id, cancellationToken)).ToDictionary(s => s.ChartId);


        var filteredCompareScoreArray = filtered
            .Where(c => phoenixScores.ContainsKey(c.Id) && phoenixScores[c.Id].Score != null)
            .OrderBy(c => phoenixScores.ContainsKey(c.Id) ? (int)(phoenixScores[c.Id]?.Score ?? 0) : 0).ToArray();
        if (!filteredCompareScoreArray.Any()) return Array.Empty<SongTierListEntry>();

        var officialScoreTierListEntries =
            (await _tierLists.GetAllEntries(request.Level >= 20 ? "Official Scores" : "Scores", cancellationToken))
            .ToDictionary(e => e.ChartId);
        var standardDeviationCompare =
            StdDev(filteredCompareScoreArray.Select(s => (int)(phoenixScores[s.Id].Score ?? 0)), true);
        var averageCompare = filteredCompareScoreArray.Average(s => phoenixScores[s.Id]?.Score ?? 0);
        var mediumMinCompare = averageCompare - standardDeviationCompare / 2;
        var easyMinCompare = averageCompare + standardDeviationCompare / 2;
        var veryEasyMinCompare = averageCompare + standardDeviationCompare;
        var oneLevelOverratedCompare = averageCompare + standardDeviationCompare * 1.5;
        var hardMinCompare = averageCompare - standardDeviationCompare;
        var veryHardMinCompare = averageCompare - standardDeviationCompare * 1.5;
        var chartIdsCompare = filteredCompareScoreArray.Select(c => c.Id).ToHashSet();
        var result = new List<SongTierListEntry>();
        foreach (var chart in filteredCompareScoreArray)
        {
            if (!officialScoreTierListEntries.TryGetValue(chart.Id, out var officialEntry)) continue;
            var score = (int)(phoenixScores[chart.Id]?.Score ?? 0);
            var myCategory = TierListCategory.Overrated;
            if (score < veryHardMinCompare)
                myCategory = TierListCategory.Underrated;
            else if (score < hardMinCompare)
                myCategory = TierListCategory.VeryHard;
            else if (score < mediumMinCompare)
                myCategory = TierListCategory.Hard;
            else if (score < easyMinCompare)
                myCategory = TierListCategory.Medium;
            else if (score < veryEasyMinCompare)
                myCategory = TierListCategory.Easy;
            else if (score < oneLevelOverratedCompare)
                myCategory = TierListCategory.VeryEasy;
            else
                myCategory = TierListCategory.Overrated;
            var diff = officialEntry.Category - myCategory;
            switch (diff)
            {
                case > 2:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Overrated,
                        diff * -100));
                    break;
                case > 1:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.VeryEasy,
                        diff * -100));
                    break;
                case > 0:
                    result.Add(
                        new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Easy, diff * -100));
                    break;
                case > -1:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Medium,
                        diff * -100));
                    break;
                case > -2:
                    result.Add(
                        new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Hard, diff * -100));
                    break;
                case > -3:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.VeryHard,
                        diff * -100));
                    break;
                default:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Underrated,
                        diff * -100));
                    break;
            }
        }

        return result;
    }
}