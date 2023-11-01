using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EllipticCurve;
using ScoreTracker.Data.Apis.Contracts;
using ScoreTracker.Data.Apis.Dtos;
using ScoreTracker.Data.Migrations;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients;

public sealed class OfficialSiteClient : IOfficialSiteClient
{
    private readonly IPiuGameApi _piuGame;
    private readonly IChartRepository _charts;

    public OfficialSiteClient(IPiuGameApi piuGame, IChartRepository charts)
    {
        _piuGame = piuGame;
        _charts = charts;
    }


    public async Task<IEnumerable<SongTierListEntry>> GetScoresLeaderboard(CancellationToken cancellationToken)
    {
        var songs = new List<PiuGameGetSongsResult.SongDto>();
        var page = 1;
        while (true)
        {
            var nextPage = await _piuGame.Get20AboveSongs(page, cancellationToken);
            songs.AddRange(nextPage.Results);
            if (nextPage.IsEnd) break;

            page++;
        }

        var averages = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var song in songs)
        {
            var scores = await _piuGame.GetSongLeaderboard(song.Id, cancellationToken);
            averages[song.Id] = (int)scores.Results.Average(r => r.Score);
        }

        var levelGroup = songs.GroupBy(s => (s.Type, s.Difficulty));
        var result = new List<SongTierListEntry>();
        foreach (var group in levelGroup)
        {
            var scores = group.Select(g => averages[g.Id]).ToArray();
            var orders = group.OrderByDescending(s => averages[s.Id]).Select((s, i) => (s, i))
                .ToDictionary(kv => kv.s.Id, kv => kv.i);
            var average = (int)scores.Average();
            var standardDev = StdDev(scores, true);
            var mediumMin = average - standardDev / 2;
            var easyMin = average + standardDev / 2;
            var veryEasyMin = average + standardDev;
            var oneLevelOverrated = average + standardDev * 1.5;
            var hardMin = average - standardDev;
            var veryHardMin = average - standardDev * 1.5;
            foreach (var song in group)
            {
                var score = averages[song.Id];
                var category = TierListCategory.Unrecorded;
                if (score < veryHardMin)
                    category = TierListCategory.Underrated;
                else if (score < hardMin)
                    category = TierListCategory.VeryHard;
                else if (score < mediumMin)
                    category = TierListCategory.Hard;
                else if (score < easyMin)
                    category = TierListCategory.Medium;
                else if (score < veryEasyMin)
                    category = TierListCategory.Easy;
                else if (score < oneLevelOverrated)
                    category = TierListCategory.VeryEasy;
                else
                    category = TierListCategory.Overrated;

                var chartType = Enum.Parse<ChartType>(song.Type);
                var chart = (await _charts.GetChartsForSong(MixEnum.Phoenix, song.Name, cancellationToken))
                    .FirstOrDefault(c => c.Type == chartType && c.Level == song.Difficulty)?.Id;
                if (chart == null) continue;

                result.Add(new SongTierListEntry("Official Scores", chart.Value, category, orders[song.Id]));
            }
        }

        return result;
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
        else
            return Math.Sqrt(sum_of_squares / values.Count());
    }
}