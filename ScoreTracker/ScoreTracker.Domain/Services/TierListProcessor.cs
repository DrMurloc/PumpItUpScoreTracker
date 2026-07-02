using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     Tier-list bucketing math (rearch Step 3.3: policy pulled out of TierListSaga).
///     Consumed by Chart Intelligence's sagas, PumbilityProjectionSaga, and the
///     ChartSkills page — a shared domain service, deliberately outside any vertical.
/// </summary>
public static class TierListProcessor
{
    public static IEnumerable<SongTierListEntry> ProcessIntoTierList(string tierListName,
        IDictionary<Guid, double> chartWeights)
    {
        if (!chartWeights.Any()) return Array.Empty<SongTierListEntry>();
        var standardDeviationCompare =
            StdDev(chartWeights.Select(s => s.Value), false);
        var averageCompare = chartWeights.Average(kv => kv.Value);
        var mediumMinCompare = averageCompare - standardDeviationCompare / 2;
        var easyMinCompare = averageCompare + standardDeviationCompare / 2;
        var veryEasyMinCompare = averageCompare + standardDeviationCompare;
        var oneLevelOverratedCompare = averageCompare + standardDeviationCompare * 1.5;
        var hardMinCompare = averageCompare - standardDeviationCompare;
        var veryHardMinCompare = averageCompare - standardDeviationCompare * 1.5;
        var result = new List<SongTierListEntry>();
        var order = 0;
        foreach (var chart in chartWeights.OrderBy(kv => kv.Value))
        {
            var score = chart.Value;
            var myCategory = TierListCategory.Overrated;
            if (score == 0)
                myCategory = TierListCategory.Unrecorded;
            else if (score < veryHardMinCompare)
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
            result.Add(new SongTierListEntry(tierListName, chart.Key, myCategory, order++));
        }

        return result;
    }

    public static IEnumerable<SongTierListEntry> ProcessIntoTierList(string tierListName,
        IDictionary<Guid, int> chartWeights)
    {
        return ProcessIntoTierList(tierListName, chartWeights.ToDictionary(kv => kv.Key, kv => (double)kv.Value));
    }

    public static double StdDev(IEnumerable<double> values,
        bool as_sample)
    {
        // Get the mean.
        double mean = values.Sum() / values.Count();

        // Get the sum of the squares of the differences
        // between the values and the mean.
        var squares_query =
            from double value in values
            select (value - mean) * (value - mean);
        var sum_of_squares = squares_query.Sum();

        if (as_sample)
            return Math.Sqrt(sum_of_squares / (values.Count() - 1));
        return Math.Sqrt(sum_of_squares / values.Count());
    }

    public static double StdDev(IEnumerable<int> values,
        bool as_sample)
    {
        return StdDev(values.Select(i => (double)i), as_sample);
    }

    public static IEnumerable<SongTierListEntry> ProcessIntoTierList(
        IDictionary<string, IDictionary<Guid, PhoenixScore>> userScores, DifficultyLevel level, string listName,
        IDictionary<string, double>? weights = null)
    {
        weights ??= userScores.ToDictionary(g => g.Key, g => 1.0);

        var includedChartIds = userScores.Values.SelectMany(kv => kv.Select(kv2 => kv2.Key)).Distinct().ToArray();
        var chartCount = includedChartIds.ToDictionary(c => c, c => 0.0);
        var chartTotal = includedChartIds.ToDictionary(c => c, c => 0.0);

        foreach (var group in userScores)
        {
            var groupName = group.Key;
            var scores = group.Value;
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
                chartCount[chart] += weights[groupName];
                if (score < veryHardMin)
                    chartTotal[chart] += 3 * weights[groupName];
                else if (score < hardMin)
                    chartTotal[chart] += 2 * weights[groupName];
                else if (score < mediumMin)
                    chartTotal[chart] += 1 * weights[groupName];
                else if (score < easyMin)
                    chartTotal[chart] += 0;
                else if (score < veryEasyMin)
                    chartTotal[chart] += -1 * weights[groupName];
                else if (score < oneLevelOverrated)
                    chartTotal[chart] += -2 * weights[groupName];
                else
                    chartTotal[chart] += -3 * weights[groupName];
            }
        }

        var averages =
            chartTotal.ToDictionary(kv => kv.Key, kv => chartTotal[kv.Key] / chartCount[kv.Key]);
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
}
