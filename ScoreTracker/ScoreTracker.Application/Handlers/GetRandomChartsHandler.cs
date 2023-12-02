using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class GetRandomChartsHandler : IRequestHandler<GetRandomChartsQuery, IEnumerable<Chart>>,
        IRequestHandler<GetIncludedRandomChartsQuery, IEnumerable<Chart>>
    {
        private readonly Random _random = new(DateTimeOffset.Now.Year + DateTimeOffset.Now.Month +
                                              DateTimeOffset.Now.Day + DateTimeOffset.Now.Hour +
                                              DateTimeOffset.Now.Minute + DateTimeOffset.Now.Second +
                                              DateTimeOffset.Now.Millisecond);

        private IChartRepository _charts;

        public GetRandomChartsHandler(IChartRepository charts)
        {
            _charts = charts;
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
                var levelWeight = chart.Type switch
                {
                    ChartType.Single or ChartType.SinglePerformance => settings.LevelWeights[chart.Level],
                    ChartType.Double or ChartType.DoublePerformance => settings.DoubleLevelWeights[chart.Level],
                    _ => settings.PlayerCountWeights[chart.PlayerCount]
                };

                if (levelWeight == 0 || settings.ChartTypeWeights[chart.Type] == 0 ||
                    settings.SongTypeWeights[chart.Song.Type] == 0)
                {
                    calculatedWeights[chart.Id] = 0;
                }
                else
                {
                    var max = 1;
                    if (levelWeight > max)
                        max = levelWeight;
                    if (settings.ChartTypeWeights[chart.Type] > max)
                        max = settings.ChartTypeWeights[chart.Type];
                    if (settings.SongTypeWeights[chart.Song.Type] > max)
                        max = settings.SongTypeWeights[chart.Song.Type];
                    calculatedWeights[chart.Id] = max;
                }
            }

            return calculatedWeights.Where(kv => kv.Value > 0);
        }

        public async Task<IEnumerable<Chart>> Handle(GetRandomChartsQuery request, CancellationToken cancellationToken)
        {
            var charts =
                (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);
            var includedCharts = GetIncludedCharts(charts, request.Settings).ToArray();
            if (includedCharts.Length < request.Settings.Count && !request.Settings.AllowRepeats)
                throw new RandomizerException("Included charts were fewer than the requried count");

            var results = new List<Chart>();
            for (var i = 0; i < request.Settings.Count; i++)
            {
                var nextGuid = NextRandomGuid(includedCharts);
                if (!request.Settings.AllowRepeats)
                    includedCharts = includedCharts.Where(kv => kv.Key != nextGuid).ToArray();
                results.Add(charts[nextGuid]);
            }

            return results;
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
    }
}
