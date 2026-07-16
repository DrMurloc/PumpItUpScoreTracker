using Microsoft.Extensions.Logging;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain;

internal sealed class WorldRankingService : IWorldRankingService
{
    private readonly IOfficialLeaderboardRepository _leaderboards;
    private readonly ILogger _logger;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _dateTime;

    public WorldRankingService(IOfficialLeaderboardRepository leaderboards, ILogger<WorldRankingService> logger,
        IChartRepository charts, IDateTimeOffsetAccessor dateTime)
    {
        _leaderboards = leaderboards;
        _logger = logger;
        _charts = charts;
        _dateTime = dateTime;
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetAll(MixEnum mix, Name username,
        CancellationToken cancellationToken)
    {
        var result = new List<RecordedPhoenixScore>();
        foreach (var record in (await _leaderboards.GetOfficialLeaderboardStatuses(mix, username, cancellationToken))
                 .Where(l => l.OfficialLeaderboardType == "Chart" && !l.LeaderboardName.Contains("CoOp"))
                 .Select(l => (l.Score, DifficultyLevel.ParseShortHand(l.LeaderboardName.Split(" ")[^1]),
                     l.LeaderboardName)))
        {
            var songName = string.Join(" ", record.LeaderboardName.Split(" ").Reverse().Skip(1).Reverse());
            var charts = await _charts.GetChartsForSong(mix, songName, cancellationToken);
            var chart = charts
                .FirstOrDefault(c => c.Type == record.Item2.chartType && c.Level == record.Item2.level);
            if (chart != null)
                result.Add(new RecordedPhoenixScore(chart.Id, record.Score, null, false, _dateTime.Now));
        }

        return result;
    }

    public async Task<IEnumerable<RecordedPhoenixScore>> GetTop50(MixEnum mix, Name username, string type,
        CancellationToken cancellationToken)
    {
        var scoringConfig = ScoringConfiguration.PumbilityScoring(mix, false);
        var result = new List<RecordedPhoenixScore>();
        foreach (var record in (await _leaderboards.GetOfficialLeaderboardStatuses(mix, username, cancellationToken))
                 .Where(l => l.OfficialLeaderboardType == "Chart" && !l.LeaderboardName.Contains("CoOp"))
                 .Select(l => (l.Score, DifficultyLevel.ParseShortHand(l.LeaderboardName.Split(" ")[^1]),
                     l.LeaderboardName))
                 .Where(l => type == "All" || (type == "Singles" && l.Item2.chartType == ChartType.Single) ||
                             (type == "Doubles" && l.Item2.chartType == ChartType.Double))
                 .OrderByDescending(l => scoringConfig.GetScore(l.Item2.level, l.Score))
                 .Take(50))
        {
            var songName = string.Join(" ", record.LeaderboardName.Split(" ").Reverse().Skip(1).Reverse());
            var charts = await _charts.GetChartsForSong(mix, songName, cancellationToken);
            var chart = charts
                .FirstOrDefault(c => c.Type == record.Item2.chartType && c.Level == record.Item2.level);
            if (chart != null)
                result.Add(new RecordedPhoenixScore(chart.Id, record.Score, null, false, _dateTime.Now));
        }

        return result;
    }
}