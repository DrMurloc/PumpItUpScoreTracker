using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application;

internal sealed class ScoreQualitySaga :
    IRequestHandler<GetPlayerScoreQualityQuery, IDictionary<Guid, ScoreRankingRecord>>,
    IRequestHandler<GetChartScoreRankingsQuery, IDictionary<Guid, ScoreRankingRecord>>,
    IRequestHandler<GetCompetitivePlayersQuery, IEnumerable<Guid>>
{
    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly IPlayerStatsReader _playerStats;
    private readonly ICurrentUserAccessor _user;
    private readonly IScoreReader _scores;

    public ScoreQualitySaga(ICurrentUserAccessor user, IPlayerStatsReader playerStats, IMemoryCache cache,
        IChartRepository charts, IScoreReader scores)
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
        var charts = await _charts.GetCharts(request.Mix, chartIds: request.ChartIds,
            cancellationToken: cancellationToken);
        var result = new Dictionary<Guid, ScoreRankingRecord>();
        foreach (var chartGroup in charts.GroupBy(c => c.Type))
        foreach (var kv in await GetRankings(request.Mix, chartGroup.Key,
                     chartGroup.Select(c => c.Id).Distinct().ToHashSet(), cancellationToken))
            result[kv.Key] = kv.Value;

        return result;
    }

    public async Task<IDictionary<Guid, ScoreRankingRecord>> Handle(GetPlayerScoreQualityQuery request,
        CancellationToken cancellationToken)
    {
        var playerScores = await GetPlayerScores(request.Mix, request.ChartType, request.Level, cancellationToken);
        var charts = (await _charts.GetCharts(request.Mix, request.Level, request.ChartType,
            cancellationToken: cancellationToken)).Select(c => c.Id).ToHashSet();
        return (await _scores.GetBestScores(request.Mix, _user.User.Id, cancellationToken))
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

    // Half-level buckets let players of similar strength share cached cohorts and
    // cohort scores; exact-level keys made every cache entry per-user, so each visitor
    // paid for their own copy of the same near-identical ledger query.
    private async Task<double> GetCompetitiveLevelBucket(MixEnum mix, ChartType chartType,
        CancellationToken cancellationToken)
    {
        var myStats = await _playerStats.GetStats(mix, _user.User.Id, cancellationToken);
        var competitiveLevel = chartType == ChartType.Single
            ? myStats.SinglesCompetitiveLevel
            : myStats.DoublesCompetitiveLevel;
        return Math.Round(competitiveLevel * 2, MidpointRounding.AwayFromZero) / 2.0;
    }

    private async Task<IEnumerable<Guid>> GetComparablePlayers(MixEnum mix, ChartType chartType,
        CancellationToken cancellationToken)
    {
        var bucket = await GetCompetitiveLevelBucket(mix, chartType, cancellationToken);
        return await _cache.GetOrCreateAsync(
            $"{nameof(ScoreQualitySaga)}__{nameof(GetComparablePlayers)}__{mix}__{bucket}__{chartType}",
            async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return (await _playerStats.GetPlayersByCompetitiveRange(mix, chartType, bucket,
                    .5, cancellationToken)).ToArray().AsEnumerable();
            }) ?? Array.Empty<Guid>();
    }

    private async Task<IDictionary<Guid, PhoenixScore[]>> GetPlayerScores(MixEnum mix, ChartType chartType,
        DifficultyLevel level, CancellationToken cancellationToken)
    {
        var bucket = await GetCompetitiveLevelBucket(mix, chartType, cancellationToken);
        return await _cache.GetOrCreateAsync(
            $"{nameof(ScoreQualitySaga)}__{nameof(GetPlayerHistoryQuery)}__{mix}__{bucket}__{level}__{chartType}",
            async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

                var players = await GetComparablePlayers(mix, chartType, cancellationToken);
                var scores = await _scores.GetPlayerScores(mix, players, chartType, level,
                    cancellationToken);
                return scores.Where(s => s.record.Score != null).GroupBy(c => c.record.ChartId)
                    .ToDictionary(g => g.Key,
                        g => g.OrderBy(s => s.record.Score).Select(s => s.record.Score!.Value).ToArray());
            }) ?? throw new ArgumentNullException("Couldn't retrieve scores from cache for score quality");
    }

    private async Task<IDictionary<Guid, ScoreRankingRecord>> GetRankings(MixEnum mix, ChartType chartType,
        ISet<Guid> chartIds,
        CancellationToken cancellationToken)
    {
        var playerScores = await GetCohortScoresByChart(mix, chartType, chartIds, cancellationToken);

        return (await _scores.GetBestScores(mix, _user.User.Id, cancellationToken))
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

    // Cohort score distributions are cached per chart (not per requested chart set) so
    // overlapping pages — home recommendations, uploads, breakdowns — hit the same
    // entries, and only genuinely unseen charts reach the ledger.
    private async Task<IReadOnlyDictionary<Guid, PhoenixScore[]>> GetCohortScoresByChart(MixEnum mix,
        ChartType chartType, ISet<Guid> chartIds, CancellationToken cancellationToken)
    {
        var bucket = await GetCompetitiveLevelBucket(mix, chartType, cancellationToken);
        var result = new Dictionary<Guid, PhoenixScore[]>();
        var missing = new List<Guid>();
        foreach (var chartId in chartIds)
            if (_cache.TryGetValue(CohortScoresKey(mix, chartType, bucket, chartId),
                    out PhoenixScore[]? cached) && cached != null)
            {
                if (cached.Length > 0) result[chartId] = cached;
            }
            else
            {
                missing.Add(chartId);
            }

        if (!missing.Any()) return result;

        var players = await GetComparablePlayers(mix, chartType, cancellationToken);
        var fetched = (await _scores.GetPlayerScores(mix, players, missing, cancellationToken))
            .GroupBy(c => c.ChartId)
            .ToDictionary(g => g.Key,
                g => g.OrderBy(s => s.Score).Select(s => s.Score).ToArray());
        foreach (var chartId in missing)
        {
            var scores = fetched.TryGetValue(chartId, out var chartScores)
                ? chartScores
                : Array.Empty<PhoenixScore>();
            // Charts nobody in the cohort has played are cached as empty so they don't
            // re-trigger the ledger query on every page load.
            _cache.Set(CohortScoresKey(mix, chartType, bucket, chartId), scores, TimeSpan.FromHours(1));
            if (scores.Length > 0) result[chartId] = scores;
        }

        return result;
    }

    private static string CohortScoresKey(MixEnum mix, ChartType chartType, double bucket, Guid chartId)
    {
        return $"{nameof(ScoreQualitySaga)}__CohortScores__{mix}__{chartType}__{bucket}__{chartId}";
    }

    public async Task<IEnumerable<Guid>> Handle(GetCompetitivePlayersQuery request, CancellationToken cancellationToken)
    {
        return await GetComparablePlayers(request.Mix, request.ChartType, cancellationToken);
    }
}