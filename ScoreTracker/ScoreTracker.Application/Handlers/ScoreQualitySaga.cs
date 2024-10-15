using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed class ScoreQualitySaga : IRequestHandler<GetPlayerScoreQualityQuery, IDictionary<Guid, double>>
    {
        private readonly ICurrentUserAccessor _user;
        private readonly IPlayerStatsRepository _playerStats;
        private readonly IMemoryCache _cache;
        private IPhoenixRecordRepository _scores;
        private readonly IChartRepository _charts;

        public ScoreQualitySaga(ICurrentUserAccessor user, IPlayerStatsRepository playerStats, IMemoryCache cache,
            IChartRepository charts, IPhoenixRecordRepository scores)
        {
            _user = user;
            _playerStats = playerStats;
            _cache = cache;
            _charts = charts;
            _scores = scores;
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

                    var myStats = await _playerStats.GetStats(userId, cancellationToken);
                    var competitiveLevel = chartType == ChartType.Double
                        ? myStats.DoublesCompetitiveLevel
                        : myStats.SinglesCompetitiveLevel;

                    var players = await _playerStats.GetPlayersByCompetitiveRange(chartType, competitiveLevel,
                        .5, cancellationToken);
                    var scores = await _scores.GetPlayerScores(players, chartType, level, cancellationToken);
                    return scores.Where(s => s.record.Score != null).GroupBy(c => c.record.ChartId)
                        .ToDictionary(g => g.Key,
                            g => g.OrderBy(s => s.record.Score).Select(s => s.record.Score!.Value).ToArray());
                }) ?? throw new ArgumentNullException("Couldn't retrieve scores from cache for score quality");
        }

        public async Task<IDictionary<Guid, double>> Handle(GetPlayerScoreQualityQuery request,
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
                        if (!playerScores.ContainsKey(c.ChartId)) return 1.0;

                        var index = playerScores[c.ChartId].Select((s, i) => (s, i))
                            .FirstOrDefault(k => k.s > c.Score, (0, -1)).i;
                        if (index == -1) return 1.0;

                        return (index - 1) / (double)playerScores[c.ChartId].Length;
                    });
        }
    }
}
