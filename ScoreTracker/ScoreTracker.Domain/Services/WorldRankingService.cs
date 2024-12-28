using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Services;

public sealed class WorldRankingService : IWorldRankingService
{
    private readonly IOfficialLeaderboardRepository _leaderboards;
    private readonly ILogger _logger;
    private readonly IChartRepository _charts;

    public WorldRankingService(IOfficialLeaderboardRepository leaderboards, ILogger<WorldRankingService> logger,
        IChartRepository charts)
    {
        _leaderboards = leaderboards;
        _logger = logger;
        _charts = charts;
    }

    public async Task CalculateWorldRankings(CancellationToken cancellationToken)
    {
        var scoringConfig = ScoringConfiguration.PumbilityScoring(false);

        var entries = (await _leaderboards.GetOfficialLeaderboardUsernames("Chart", cancellationToken)).ToArray();
        await _leaderboards.DeleteWorldRankings(cancellationToken);
        var max = entries.Count();
        var current = 1;
        foreach (var username in entries)
        {
            _logger.LogInformation($"User {current++}/{max}");
            foreach (var type in new[] { "Singles", "Doubles", "All" })
            {
                var statuses = (await _leaderboards.GetOfficialLeaderboardStatuses(username, cancellationToken))
                    .Where(l => l.OfficialLeaderboardType == "Chart" && !l.LeaderboardName.Contains("CoOp"))
                    .Select(l => (l.Score, DifficultyLevel.ParseShortHand(l.LeaderboardName.Split(" ")[^1])))
                    .ToArray();
                var records = statuses
                    .Where(l => type == "All" || (type == "Singles" && l.Item2.chartType == ChartType.Single) ||
                                (type == "Doubles" && l.Item2.chartType == ChartType.Double))
                    .OrderByDescending(l => scoringConfig.GetScore(l.Item2.level, l.Score))
                    .Take(50)
                    .ToArray();
                if (!records.Any()) continue;
                var singlesCount = 0;
                var doublesCount = 0;
                var totalRating = 0;
                var totalDifficulty = 0;
                var totalScore = 0;
                foreach (var record in records)
                {
                    switch (record.Item2.chartType)
                    {
                        case ChartType.Single:
                            singlesCount++;
                            break;
                        case ChartType.Double:
                            doublesCount++;
                            break;
                    }

                    totalRating += (int)scoringConfig.GetScore(record.Item2.level, record.Score);
                    totalDifficulty += record.Item2.level;
                    totalScore += record.Score;
                }

                var totalCount = singlesCount + doublesCount;
                var compOrdered = statuses.OrderByDescending(u =>
                    ScoringConfiguration.CalculateFungScore(u.Item2.level, u.Score, u.Item2.chartType)).ToArray();
                var competitive =
                    compOrdered
                        .Take(100).Sum(u =>
                            ScoringConfiguration.CalculateFungScore(u.Item2.level, u.Score, u.Item2.chartType)) /
                    100.0;
                var singles = compOrdered.Where(u => u.Item2.chartType == ChartType.Single).Take(50)
                                  .Sum(u => ScoringConfiguration.CalculateFungScore(u.Item2.level, u.Score,
                                      u.Item2.chartType)) /
                              50.0;
                var doubles = compOrdered.Where(u => u.Item2.chartType == ChartType.Double).Take(50)
                                  .Sum(u => ScoringConfiguration.CalculateFungScore(u.Item2.level, u.Score,
                                      u.Item2.chartType))
                              / 50.0;

                await _leaderboards.SaveWorldRanking(new WorldRankingRecord(username, type,
                    totalDifficulty / (double)totalCount, (int)(totalScore / (double)totalCount), singlesCount,
                    doublesCount,
                    totalRating, competitive, singles, doubles), cancellationToken);
            }
        }
    }

    private static double AvgOr0(double[] charts)
    {
        return charts.Any() ? charts.Average() : 0;
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetAll(Name username, CancellationToken cancellationToken)
    {
        var result = new List<RecordedPhoenixScore>();
        foreach (var record in (await _leaderboards.GetOfficialLeaderboardStatuses(username, cancellationToken))
                 .Where(l => l.OfficialLeaderboardType == "Chart" && !l.LeaderboardName.Contains("CoOp"))
                 .Select(l => (l.Score, DifficultyLevel.ParseShortHand(l.LeaderboardName.Split(" ")[^1]),
                     l.LeaderboardName)))
        {
            var songName = string.Join(" ", record.LeaderboardName.Split(" ").Reverse().Skip(1).Reverse());
            var charts = await _charts.GetChartsForSong(MixEnum.Phoenix, songName, cancellationToken);
            var chart = charts
                .FirstOrDefault(c => c.Type == record.Item2.chartType && c.Level == record.Item2.level);
            if (chart != null)
                result.Add(new RecordedPhoenixScore(chart.Id, record.Score, null, false, DateTimeOffset.Now));
        }

        return result;
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetTop50(Name username, string type,
        CancellationToken cancellationToken)
    {
        var scoringConfig = ScoringConfiguration.PumbilityScoring(false);
        var result = new List<RecordedPhoenixScore>();
        foreach (var record in (await _leaderboards.GetOfficialLeaderboardStatuses(username, cancellationToken))
                 .Where(l => l.OfficialLeaderboardType == "Chart" && !l.LeaderboardName.Contains("CoOp"))
                 .Select(l => (l.Score, DifficultyLevel.ParseShortHand(l.LeaderboardName.Split(" ")[^1]),
                     l.LeaderboardName))
                 .Where(l => type == "All" || (type == "Singles" && l.Item2.chartType == ChartType.Single) ||
                             (type == "Doubles" && l.Item2.chartType == ChartType.Double))
                 .OrderByDescending(l => scoringConfig.GetScore(l.Item2.level, l.Score))
                 .Take(50))
        {
            var songName = string.Join(" ", record.LeaderboardName.Split(" ").Reverse().Skip(1).Reverse());
            var charts = await _charts.GetChartsForSong(MixEnum.Phoenix, songName, cancellationToken);
            var chart = charts
                .FirstOrDefault(c => c.Type == record.Item2.chartType && c.Level == record.Item2.level);
            if (chart != null)
                result.Add(new RecordedPhoenixScore(chart.Id, record.Score, null, false, DateTimeOffset.Now));
        }

        return result;
    }
}