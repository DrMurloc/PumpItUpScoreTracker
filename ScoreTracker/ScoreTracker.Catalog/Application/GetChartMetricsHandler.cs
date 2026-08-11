using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     The raw piucenter passthrough. The repository read is already cached; what is memoized
///     here is the reshape into per-chart lookups, which the source dictionary is not — an
///     export prices four thousand charts through it and would otherwise scan a list per row.
/// </summary>
internal sealed class GetChartMetricsHandler
    : IRequestHandler<GetChartMetricsQuery, IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, decimal>>>,
        IRequestHandler<GetChartMetricNamesQuery, IReadOnlyList<string>>
{
    private const string MapCacheKey = nameof(GetChartMetricsHandler) + "__Map";
    private const string NamesCacheKey = nameof(GetChartMetricsHandler) + "__Names";

    private readonly IMemoryCache _cache;
    private readonly IChartSkillMetricRepository _metrics;

    public GetChartMetricsHandler(IChartSkillMetricRepository metrics, IMemoryCache cache)
    {
        _metrics = metrics;
        _cache = cache;
    }

    public async Task<IReadOnlyList<string>> Handle(GetChartMetricNamesQuery request,
        CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync(NamesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            var map = await Handle(new GetChartMetricsQuery(), cancellationToken);
            return (IReadOnlyList<string>)map.Values
                .SelectMany(m => m.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
        }))!;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, decimal>>> Handle(
        GetChartMetricsQuery request, CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync(MapCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheFor;
            var banked = await _metrics.GetMetricsByChart(PiuCenterMetrics.Source, cancellationToken);
            return (IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, decimal>>)banked.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<string, decimal>)kv.Value
                    // A crawl can bank the same name twice for a chart; last write wins rather
                    // than throwing an export away over a duplicate key.
                    .GroupBy(m => m.MetricName, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal));
        }))!;
    }

    /// <summary>The crawl runs daily; a day-long memo cannot outlive a data version by much.</summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);
}
