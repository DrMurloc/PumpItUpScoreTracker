using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Caching;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Infrastructure;

internal sealed class EFMixVersionRepository : IMixVersionRepository
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFMixVersionRepository(IMemoryCache cache, IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _cache = cache;
        _factory = factory;
    }

    public async Task<IReadOnlyList<MixVersion>> GetVersions(MixEnum mix, CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        return (await _cache.GetOrCreateAsync(VersionsKey(mixId), async entry =>
        {
            // A catalog fact the whole population shares, so a Mix key; the one write below
            // evicts it, and a fortnight covers the case nothing writes.
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            IReadOnlyList<MixVersion> versions = await database.Set<MixVersionEntity>()
                .Where(v => v.MixId == mixId)
                .OrderBy(v => v.SortOrder).ThenBy(v => v.Name)
                .Select(v => new MixVersion(v.Id, mix, v.Name, v.ReleaseDate, v.SortOrder))
                .ToListAsync(cancellationToken);
            return versions;
        }))!;
    }

    public async Task<Guid> Create(MixEnum mix, string name, DateOnly? releaseDate,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        var trimmed = name.Trim();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Set<MixVersionEntity>()
            .FirstOrDefaultAsync(v => v.MixId == mixId && v.Name == trimmed, cancellationToken);
        if (existing != null) return existing.Id;

        var last = await database.Set<MixVersionEntity>()
            .Where(v => v.MixId == mixId)
            .Select(v => (int?)v.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;
        var entity = new MixVersionEntity
        {
            Id = Guid.NewGuid(),
            MixId = mixId,
            Name = trimmed,
            ReleaseDate = releaseDate,
            SortOrder = last + 10
        };
        await database.Set<MixVersionEntity>().AddAsync(entity, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        _cache.Remove(VersionsKey(mixId));
        return entity.Id;
    }

    private static string VersionsKey(Guid mixId)
    {
        return CacheKeys.Mix(nameof(EFMixVersionRepository), mixId, nameof(GetVersions));
    }
}
