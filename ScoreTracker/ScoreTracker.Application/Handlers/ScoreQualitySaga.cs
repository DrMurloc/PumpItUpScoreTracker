using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers;

public sealed class ScoreQualitySaga :
    IRequestHandler<GetPlayerScoreQualityQuery, IDictionary<Guid, ScoreRankingRecord>>,
    IRequestHandler<GetChartScoreRankingsQuery, IDictionary<Guid, ScoreRankingRecord>>,
    IRequestHandler<GetCompetitivePlayersQuery, IEnumerable<Guid>>
{
    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly IPlayerStatsRepository _playerStats;
    private readonly ICurrentUserAccessor _user;
    private readonly IPhoenixRecordRepository _scores;

    public ScoreQualitySaga(ICurrentUserAccessor user, IPlayerStatsRepository playerStats, IMemoryCache cache,
        IChartRepository charts, IPhoenixRecordRepository scores)
    {
        _user = user;
        _playerStats = playerStats;
        _cache = cache;
        _charts = charts;
        _scores = scores;
    }

    public async Task<IDictionary<Guid, ScoreRankingRecord>> Handle(GetChartScoreRankingsQuery request,
        CancellationToken cancellationToken)
    {
        var charts = await _charts.GetCharts(MixEnum.Phoenix, chartIds: request.ChartIds,
            cancellationToken: cancellationToken);
        var result = new Dictionary<Guid, ScoreRankingRecord>();
        foreach (var chartGroup in charts.GroupBy(c => c.Type))
        foreach (var kv in await GetRankings(chartGroup.Key,
                     chartGroup.Select(c => c.Id).Distinct().ToHashSet(), cancellationToken))
            result[kv.Key] = kv.Value;

        return result;
    }

    public async Task<IDictionary<Guid, ScoreRankingRecord>> Handle(GetPlayerScoreQualityQuery request,
        CancellationToken cancellationToken)
    {
        var playerScores = await GetPlayerScores(request.ChartType, request.Level, cancellationToken);
        var charts = (await _charts.GetCharts(MixEnum.Phoenix, request.Level, request.ChartType,
            cancellationToken: cancellationToken)).Select(c => c.Id).ToHashSet();
        return (await _scores.GetRecordedScores(_user.User.Id, cancellationToken))
            .Where(s => charts.Contains(s.ChartId))
            .Where(s => s.Score != null)
            .ToDictionary(c => c.ChartId,
                c =>
                {
                    if (!playerScores.ContainsKey(c.ChartId))
                        return new ScoreRankingRecord(1.0, 1);

                    var index = playerScores[c.ChartId].Select((s, i) => (s, i))
                        .FirstOrDefault(k => k.s > c.Score, (0, -1)).i;
                    if (index == -1) return new ScoreRankingRecord(1.0, playerScores[c.ChartId].Length);
                    if (index == 1)
                        return
                            new ScoreRankingRecord(0.0,
                                playerScores[c.ChartId]
                                    .Length); //We want 1st place to show as 100%, including you as better than yourself but last place to show as 0%
                    return new ScoreRankingRecord(index / (double)playerScores[c.ChartId].Length,
                        playerScores[c.ChartId].Length);
                });
    }

    private async Task<IEnumerable<Guid>> GetComparablePlayers(ChartType chartType,
        CancellationToken cancellationToken)
    {
        var myStats = await _playerStats.GetStats(_user.User.Id, cancellationToken);
        var competitiveLevel = chartType == ChartType.Single
            ? myStats.SinglesCompetitiveLevel
            : myStats.DoublesCompetitiveLevel;

        return await _playerStats.GetPlayersByCompetitiveRange(chartType, competitiveLevel,
            .5, cancellationToken);
    }

    private async Task<IDictionary<Guid, PhoenixScore[]>> GetPlayerScores(ChartType chartType,
        DifficultyLevel level, CancellationToken cancellationToken)
    {
        var userId = _user.User.Id;
        return await _cache.GetOrCreateAsync(
            $"{nameof(ScoreQualitySaga)}__{nameof(GetPlayerHistoryQuery)}__{userId}__{level}__{chartType}",
            async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

                var players = await GetComparablePlayers(chartType, cancellationToken);
                var scores = await _scores.GetPlayerScores(players, chartType, level, cancellationToken);
                return scores.Where(s => s.record.Score != null).GroupBy(c => c.record.ChartId)
                    .ToDictionary(g => g.Key,
                        g => g.OrderBy(s => s.record.Score).Select(s => s.record.Score!.Value).ToArray());
            }) ?? throw new ArgumentNullException("Couldn't retrieve scores from cache for score quality");
    }

    private async Task<IDictionary<Guid, ScoreRankingRecord>> GetRankings(ChartType chartType, ISet<Guid> chartIds,
        CancellationToken cancellationToken)
    {
        var players = await GetComparablePlayers(chartType, cancellationToken);
        var playerScores = (await _scores.GetPlayerScores(players, chartIds, cancellationToken))
            .GroupBy(c => c.ChartId)
            .ToDictionary(g => g.Key,
                g => g.OrderBy(s => s.Score).Select(s => s.Score).ToArray());

        return (await _scores.GetRecordedScores(_user.User.Id, cancellationToken))
            .Where(s => chartIds.Contains(s.ChartId))
            .Where(s => s.Score != null)
            .ToDictionary(c => c.ChartId,
                c =>
                {
                    if (!playerScores.ContainsKey(c.ChartId))
                        return new ScoreRankingRecord(1.0, 1);

                    var index = playerScores[c.ChartId].Select((s, i) => (s, i))
                        .FirstOrDefault(k => k.s > c.Score, (0, -1)).i;
                    if (index == -1) return new ScoreRankingRecord(1.0, playerScores[c.ChartId].Length);
                    if (index == 1)
                        return
                            new ScoreRankingRecord(0.0,
                                playerScores[c.ChartId]
                                    .Length); //We want 1st place to show as 100%, including you as better than yourself but last place to show as 0%
                    return new ScoreRankingRecord(index / (double)playerScores[c.ChartId].Length,
                        playerScores[c.ChartId].Length);
                });
    }

    public async Task<IEnumerable<Guid>> Handle(GetCompetitivePlayersQuery request, CancellationToken cancellationToken)
    {
        return await GetComparablePlayers(request.ChartType, cancellationToken);
    }
}