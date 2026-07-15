using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Catalog.Infrastructure;

/// <summary>
///     Crawl metrics, held in memory per source.
///     These rows describe what a chart <em>is</em> — every badge fraction, the top-3 picks, NPS,
///     sustain time, time under tension — and they change only when a crawl replaces them. Reading
///     them per request was pure waste: the similarity job sweeps the whole set nightly, and the
///     similar-charts filters count against the whole pool (47.5k rows for Doubles alone) on every
///     adjustment. Cached whole and sliced in memory, because every caller wants a different subset
///     of the same unchanging set.
///     **Eviction is exact rather than timed.** <see cref="ReplaceChartMetrics" /> is the only way
///     these rows change, so it drops the source's entry and the next read rebuilds. A crawl writes
///     per chart and therefore evicts per chart — harmless, because the rebuild happens on the next
///     read and a crawl does not read.
/// </summary>
internal sealed class EFChartSkillMetricRepository : IChartSkillMetricRepository
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartSkillMetricRepository(IMemoryCache cache, IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _cache = cache;
        _factory = factory;
    }

    private static string CacheKey(string source)
    {
        return $"ChartSkillMetrics__{source}";
    }

    public async Task ReplaceChartMetrics(Guid chartId, string source, IEnumerable<ChartSkillMetric> metrics,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Set<ChartSkillMetricEntity>()
            .Where(e => e.ChartId == chartId && e.Source == source)
            .ToArrayAsync(cancellationToken);
        database.Set<ChartSkillMetricEntity>().RemoveRange(existing);
        await database.Set<ChartSkillMetricEntity>().AddRangeAsync(metrics.Select(m => new ChartSkillMetricEntity
        {
            ChartId = chartId,
            Source = source,
            MetricName = m.MetricName,
            Value = m.Value,
            Grade = m.Grade
        }), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey(source));
    }

    public async Task<IReadOnlyList<ChartSkillMetric>> GetMetrics(IEnumerable<Guid> chartIds, string source,
        CancellationToken cancellationToken = default)
    {
        var bySource = await GetAllMetrics(source, cancellationToken);
        return chartIds.Distinct()
            .SelectMany(id => bySource.TryGetValue(id, out var metrics)
                ? metrics
                : Array.Empty<ChartSkillMetric>())
            .ToArray();
    }

    public async Task<IReadOnlySet<Guid>> GetChartIdsWithMetrics(string source,
        CancellationToken cancellationToken = default)
    {
        return (await GetAllMetrics(source, cancellationToken)).Keys.ToHashSet();
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>>> GetAllMetrics(string source,
        CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync(CacheKey(source), async entry =>
        {
            // A backstop, not the mechanism — a write is what evicts.
            entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return (IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillMetric>>)
                (await database.Set<ChartSkillMetricEntity>()
                    .Where(e => e.Source == source)
                    .ToArrayAsync(cancellationToken))
                .GroupBy(e => e.ChartId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<ChartSkillMetric>)g
                    .Select(e => new ChartSkillMetric(e.ChartId, e.MetricName, e.Value, e.Grade))
                    .ToArray());
        }))!;
    }
}
