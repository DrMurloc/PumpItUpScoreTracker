using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers
{
    public sealed class RandomizerSaga : IRequestHandler<GetRandomChartsQuery, IEnumerable<Chart>>,
        IRequestHandler<GetIncludedRandomChartsQuery, IEnumerable<Chart>>,
        IRequestHandler<SaveUserRandomSettingsCommand>,
        IRequestHandler<DeleteRandomSettingsCommand>
    {
        private readonly Random _random = new(DateTimeOffset.Now.Year + DateTimeOffset.Now.Month +
                                              DateTimeOffset.Now.Day + DateTimeOffset.Now.Hour +
                                              DateTimeOffset.Now.Minute + DateTimeOffset.Now.Second +
                                              DateTimeOffset.Now.Millisecond);

        private IChartRepository _charts;
        private readonly IRandomizerRepository _repo;
        private readonly ICurrentUserAccessor _currentUser;

        public RandomizerSaga(IChartRepository charts,
            IRandomizerRepository repo,
            ICurrentUserAccessor currentUser)
        {
            _charts = charts;
            _repo = repo;
            _currentUser = currentUser;
        }

        private Guid NextRandomGuid(IEnumerable<KeyValuePair<Guid, int>> weights)
        {
            var cur = 0;
            var distribution = new Dictionary<int, Guid>();
            foreach (var weight in weights)
                for (var i = 0; i < weight.Value; i++)
                {
                    distribution[cur] = weight.Key;
                    cur++;
                }

            var result = _random.Next(cur);
            return distribution[result];
        }

        private IEnumerable<KeyValuePair<Guid, int>> GetIncludedCharts(IDictionary<Guid, Chart> charts,
            RandomSettings settings)
        {
            var calculatedWeights = new Dictionary<Guid, int>();
            foreach (var chart in charts.Values)
            {
                if (settings.UseScoringLevels && chart.ScoringLevel == null)
                {
                    calculatedWeights[chart.Id] = 0;
                    continue;
                }

                var level = settings.UseScoringLevels
                    ? (int)Math.Floor(chart.ScoringLevel!.Value)
                    : (int)chart.Level;
                var levelWeight = chart.Type switch
                {
                    ChartType.Single or ChartType.SinglePerformance => settings.LevelWeights[level],
                    ChartType.Double or ChartType.DoublePerformance => settings.DoubleLevelWeights[level],
                    _ => settings.PlayerCountWeights[chart.PlayerCount]
                };

                if (levelWeight == 0 ||
                    settings.SongTypeWeights[chart.Song.Type] == 0)
                {
                    calculatedWeights[chart.Id] = 0;
                }
                else
                {
                    var max = 1;
                    if (levelWeight > max)
                        max = levelWeight;

                    if (settings.SongTypeWeights[chart.Song.Type] > max)
                        max = settings.SongTypeWeights[chart.Song.Type];
                    calculatedWeights[chart.Id] = max;
                }
            }

            return calculatedWeights.Where(kv => kv.Value > 0);
        }

        public async Task<IEnumerable<Chart>> Handle(GetRandomChartsQuery request, CancellationToken cancellationToken)
        {
            var results = new List<Chart>();

            foreach (var typeMinimum in request.Settings.ChartTypeMinimums.Where(kv => kv.Value != null))
            {
                var newSettings = request.Settings with
                {
                    Count = typeMinimum.Value!.Value
                };

                newSettings.ClearLevelWeights();
                if (typeMinimum.Key == ChartType.Single)
                    newSettings.LevelWeights = request.Settings.LevelWeights.ToDictionary();

                if (typeMinimum.Key == ChartType.Double)
                    newSettings.DoubleLevelWeights = request.Settings.DoubleLevelWeights.ToDictionary();

                if (typeMinimum.Key == ChartType.CoOp)
                    newSettings.PlayerCountWeights = request.Settings.PlayerCountWeights.ToDictionary();
                newSettings.ClearChartTypeLevelMinimums();
                newSettings.ClearLevelMinimums();
                newSettings.ClearChartTypeMinimums();
                newSettings.ClearCustomMinimums();
                var result = await Handle(new GetRandomChartsQuery(newSettings), cancellationToken);
                results.AddRange(result);
            }

            foreach (var levelMinimum in request.Settings.LevelMinimums.Where(kv => kv.Value != null))
            {
                var newSettings = request.Settings with
                {
                    Count = levelMinimum.Value!.Value,
                    LevelWeights = request.Settings.LevelWeights.ToDictionary(kv => kv.Key,
                        kv => kv.Value > 0 && kv.Key == levelMinimum.Key ? 1 : 0),
                    DoubleLevelWeights = request.Settings.DoubleLevelWeights.ToDictionary(kv => kv.Key,
                        kv => kv.Value > 0 && kv.Key == levelMinimum.Key ? 1 : 0),
                    PlayerCountWeights = request.Settings.PlayerCountWeights.ToDictionary(kv => kv.Key,
                        kv => kv.Value > 0 && kv.Key == levelMinimum.Key ? 1 : 0)
                };

                newSettings.ClearChartTypeLevelMinimums();
                newSettings.ClearLevelMinimums();
                newSettings.ClearChartTypeMinimums();
                newSettings.ClearCustomMinimums();
                var result = await Handle(new GetRandomChartsQuery(newSettings), cancellationToken);
                results.AddRange(result);
            }

            foreach (var clMinimum in request.Settings.ChartTypeLevelMinimums.Where(kv => kv.Value != null))
            {
                var (chartType, level) = DifficultyLevel.ParseShortHand(clMinimum.Key);
                var newSettings = request.Settings with
                {
                    Count = clMinimum.Value!.Value
                };
                newSettings.ClearLevelWeights();
                newSettings.ClearChartTypeLevelMinimums();
                newSettings.ClearChartTypeMinimums();
                newSettings.ClearCustomMinimums();
                if (chartType == ChartType.Single)
                    newSettings.LevelWeights = DifficultyLevel.All.ToDictionary(t => (int)t, t => t == level ? 1 : 0);


                if (chartType == ChartType.Double)
                    newSettings.DoubleLevelWeights =
                        DifficultyLevel.All.ToDictionary(t => (int)t, t => t == level ? 1 : 0);

                if (chartType == ChartType.CoOp)
                    newSettings.PlayerCountWeights =
                        newSettings.PlayerCountWeights.ToDictionary(kv => kv.Key, kv => kv.Key == level ? 1 : 0);
                var result = await Handle(new GetRandomChartsQuery(newSettings), cancellationToken);
                results.AddRange(result);
            }

            foreach (var customMinimum in request.Settings.CustomMinimums.Where(kv => kv.Value > 0))
            {
                if (!LevelBucket.TryParse(customMinimum.Key, out var bucket)) continue;
                var newSettings = request.Settings with
                {
                    Count = customMinimum.Value
                };
                newSettings.ClearLevelWeights();
                newSettings.ClearChartTypeLevelMinimums();
                newSettings.ClearChartTypeMinimums();
                newSettings.ClearCustomMinimums();
                foreach (var (chartType, level) in bucket.GetDifficulties())
                {
                    if (chartType == ChartType.Single)
                        newSettings.LevelWeights[level] = customMinimum.Value;
                    if (chartType == ChartType.Double)
                        newSettings.DoubleLevelWeights[level] = customMinimum.Value;
                    if (chartType == ChartType.CoOp)
                        newSettings.PlayerCountWeights[level] = customMinimum.Value;
                    var result = await Handle(new GetRandomChartsQuery(newSettings), cancellationToken);
                    results.AddRange(result);
                }
            }

            var charts =
                (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);
            var includedCharts = GetIncludedCharts(charts, request.Settings).ToArray();
            if (includedCharts.Length < request.Settings.Count && !request.Settings.AllowRepeats)
                return includedCharts.Select(c => charts[c.Key]);

            var remaining = request.Settings.Count - results.Count;

            for (var i = 0; i < remaining; i++)
            {
                var nextGuid = NextRandomGuid(includedCharts);
                if (!request.Settings.AllowRepeats)
                    includedCharts = includedCharts.Where(kv => kv.Key != nextGuid).ToArray();
                results.Add(charts[nextGuid]);
            }

            switch (request.Settings.Ordering)
            {
                case RandomSettings.ResultsOrdering.DecreasingLevel:
                    return results.OrderByDescending(r => r.Level).ToArray();
                case RandomSettings.ResultsOrdering.IncreasingLevel:
                    return results.OrderBy(r => r.Level).ToArray();
                default:
                case RandomSettings.ResultsOrdering.Randomized:
                    return results.OrderBy(r => _random.NextDouble()).ToArray();
            }
        }

        public async Task<IEnumerable<Chart>> Handle(GetIncludedRandomChartsQuery request,
            CancellationToken cancellationToken)
        {
            var charts =
                (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);
            var includedCharts = GetIncludedCharts(charts, request.Settings).ToArray();
            return includedCharts.Select(kv => charts[kv.Key]).ToArray();
        }


        public async Task Handle(DeleteRandomSettingsCommand request, CancellationToken cancellationToken)
        {
            await _repo.DeleteSettings(_currentUser.User.Id, request.SettingsName, cancellationToken);
        }

        public async Task Handle(SaveUserRandomSettingsCommand request, CancellationToken cancellationToken)
        {
            await _repo.SaveSettings(_currentUser.User.Id, request.SettingsName, request.Settings, cancellationToken);
        }
    }
}
