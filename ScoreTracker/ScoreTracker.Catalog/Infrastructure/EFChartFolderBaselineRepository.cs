using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Infrastructure;

/// <summary>
///     Folder baselines, held in memory per mix. They change only when a rebuild replaces
///     them, and every chip on every surface reads one — so the whole mix is cached and
///     sliced in memory, and a write evicts exactly what it wrote.
/// </summary>
internal sealed class EFChartFolderBaselineRepository : IChartFolderBaselineRepository
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartFolderBaselineRepository(IMemoryCache cache, IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _cache = cache;
        _factory = factory;
    }

    private static string CacheKey(MixEnum mix)
    {
        return $"ChartFolderBaselines__{mix}";
    }

    public async Task ReplaceBaselines(MixEnum mix, IEnumerable<ChartFolderBaseline> baselines,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ChartFolderBaselineEntity>().Where(e => e.MixId == mixId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<ChartFolderBaselineEntity>().AddRangeAsync(baselines.Select(b =>
            new ChartFolderBaselineEntity
            {
                MixId = mixId,
                ChartType = b.Type.ToString(),
                Level = b.Level,
                Badge = b.Badge,
                CoreCutoff = b.CoreCutoff,
                QualifiedCount = b.QualifiedCount,
                AnalyzedCharts = b.AnalyzedCharts
            }), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey(mix));
    }

    public async Task<IReadOnlyDictionary<string, ChartFolderBaseline>> GetFolderBaselines(MixEnum mix,
        ChartType type, int level, CancellationToken cancellationToken = default)
    {
        var all = await GetAll(mix, cancellationToken);
        return all.TryGetValue((type, level), out var folder)
            ? folder
            : new Dictionary<string, ChartFolderBaseline>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<(ChartType, int), IReadOnlyDictionary<string, ChartFolderBaseline>>>
        GetAll(MixEnum mix, CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync(CacheKey(mix), async entry =>
        {
            // A backstop, not the mechanism — a rebuild is what evicts.
            entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
            var mixId = MixIds.For(mix);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var rows = await database.Set<ChartFolderBaselineEntity>()
                .Where(e => e.MixId == mixId)
                .ToArrayAsync(cancellationToken);
            return (IReadOnlyDictionary<(ChartType, int), IReadOnlyDictionary<string, ChartFolderBaseline>>)rows
                .Select(e => new ChartFolderBaseline(mix, Enum.Parse<ChartType>(e.ChartType), e.Level, e.Badge,
                    e.CoreCutoff, e.QualifiedCount, e.AnalyzedCharts))
                .GroupBy(b => (b.Type, b.Level))
                .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, ChartFolderBaseline>)g
                    .ToDictionary(b => b.Badge, b => b, StringComparer.OrdinalIgnoreCase));
        }))!;
    }
}
