using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Catalog.Infrastructure;

/// <summary>
///     Banked step timelines, cached per chart — a page view reads exactly one chart, unlike
///     the whole-source sweeps the metric cache serves. Replace is chunked with a fresh context
///     per chunk (an ingestion is thousands of rows) and evicts each written chart once, after
///     its write lands.
/// </summary>
internal sealed class EFChartStepChartRepository : IChartStepChartRepository
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartStepChartRepository(IMemoryCache cache, IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _cache = cache;
        _factory = factory;
    }

    private static string CacheKey(Guid chartId)
    {
        return $"ChartStepChart__{chartId}";
    }

    public async Task Replace(IReadOnlyDictionary<Guid, BankedStepChart> banked,
        CancellationToken cancellationToken = default)
    {
        foreach (var chunk in banked.Chunk(200))
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var chartIds = chunk.Select(kv => kv.Key).ToArray();
            var existing = await database.Set<ChartStepChartEntity>()
                .Where(e => chartIds.Contains(e.ChartId))
                .ToDictionaryAsync(e => e.ChartId, cancellationToken);
            foreach (var (chartId, row) in chunk)
                if (existing.TryGetValue(chartId, out var entity))
                {
                    entity.Vintage = row.Vintage;
                    entity.UpdatedAt = row.UpdatedAt;
                    entity.Payload = row.Payload;
                }
                else
                {
                    database.Set<ChartStepChartEntity>().Add(new ChartStepChartEntity
                    {
                        ChartId = chartId,
                        Vintage = row.Vintage,
                        UpdatedAt = row.UpdatedAt,
                        Payload = row.Payload
                    });
                }

            await database.SaveChangesAsync(cancellationToken);
            foreach (var (chartId, _) in chunk) _cache.Remove(CacheKey(chartId));
        }
    }

    public async Task<IReadOnlyList<Guid>> GetBankedChartIds(CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ChartStepChartEntity>()
            .Select(e => e.ChartId)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<BankedStepChart?> Get(Guid chartId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(CacheKey(chartId), async entry =>
        {
            // A backstop, not the mechanism — a write is what evicts.
            entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await database.Set<ChartStepChartEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ChartId == chartId, cancellationToken);
            return entity == null
                ? null
                : new BankedStepChart(entity.Vintage, entity.UpdatedAt, entity.Payload);
        });
    }
}
