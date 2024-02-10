using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Services
{
    public sealed class WorldRankingService : IWorldRankingService
    {
        private readonly IOfficialLeaderboardRepository _leaderboards;
        private readonly ILogger _logger;

        public WorldRankingService(IOfficialLeaderboardRepository leaderboards, ILogger<WorldRankingService> logger)
        {
            _leaderboards = leaderboards;
            _logger = logger;
        }

        public async Task CalculateWorldRankings(CancellationToken cancellationToken)
        {
            var scoringConfig = new ScoringConfiguration
            {
                ContinuousLetterGradeScale = true
            };
            var entries = (await _leaderboards.GetOfficialLeaderboardUsernames("Chart", cancellationToken)).ToArray();
            await _leaderboards.DeleteWorldRankings(cancellationToken);
            var max = entries.Count();
            var current = 1;
            foreach (var username in entries)
            {
                _logger.LogInformation($"User {current++}/{max}");
                foreach (var type in new[] { "Singles", "Doubles", "All" })
                {
                    var records = (await _leaderboards.GetOfficialLeaderboardStatuses(username, cancellationToken))
                        .Where(l => l.OfficialLeaderboardType == "Chart" && !l.LeaderboardName.Contains("CoOp"))
                        .Select(l => (l.Score, DifficultyLevel.ParseShortHand(l.LeaderboardName.Split(" ")[^1])))
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

                        totalRating += scoringConfig.GetScore(record.Item2.level, record.Score);
                        totalDifficulty += record.Item2.level;
                        totalScore += record.Score;
                    }

                    var totalCount = singlesCount + doublesCount;
                    await _leaderboards.SaveWorldRanking(new WorldRankingRecord(username, type,
                        totalDifficulty / (double)totalCount, (int)(totalScore / (double)totalCount), singlesCount,
                        doublesCount,
                        totalRating), cancellationToken);
                }
            }
        }
    }
}
