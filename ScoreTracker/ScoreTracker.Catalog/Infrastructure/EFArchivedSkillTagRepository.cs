using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Catalog.Infrastructure;

/// <summary>
///     The archived hand tags, cached whole and forever. Nothing writes this table any more —
///     it was filled once by the PiuCenterAliasAndMetrics migration — so there is nothing to
///     invalidate and the whole set is small enough to hold.
/// </summary>
internal sealed class EFArchivedSkillTagRepository : IArchivedSkillTagRepository
{
    private const string CacheKey = "ArchivedSkillTags";

    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFArchivedSkillTagRepository(IMemoryCache cache, IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _cache = cache;
        _factory = factory;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetArchivedTags(IEnumerable<Guid> chartIds,
        CancellationToken cancellationToken = default)
    {
        var all = await GetAll(cancellationToken);
        return chartIds.Distinct()
            .Where(all.ContainsKey)
            .ToDictionary(id => id, id => all[id]);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetAll(CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return (IReadOnlyDictionary<Guid, IReadOnlyList<string>>)(await database
                    .Set<ChartSkillArchiveEntity>()
                    .ToArrayAsync(cancellationToken))
                .GroupBy(e => e.ChartId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g
                    .OrderByDescending(e => e.IsHighlighted)
                    .ThenBy(e => e.SkillName, StringComparer.OrdinalIgnoreCase)
                    .Select(e => ChabalaSkillNames.DisplayName(e.SkillName))
                    .Distinct()
                    .ToArray());
        }))!;
    }
}
