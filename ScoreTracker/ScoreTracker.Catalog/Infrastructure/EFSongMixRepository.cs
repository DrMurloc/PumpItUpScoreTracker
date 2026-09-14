using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Infrastructure;

internal sealed class EFSongMixRepository : ISongMixRepository
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFSongMixRepository(IMemoryCache cache, IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _cache = cache;
        _factory = factory;
    }

    public async Task SetChannel(MixEnum mix, Guid songId, Channel channel,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var row = await database.Set<SongMixEntity>()
            .FirstOrDefaultAsync(r => r.SongId == songId && r.MixId == mixId, cancellationToken);
        if (row == null)
        {
            row = new SongMixEntity { SongId = songId, MixId = mixId };
            await database.Set<SongMixEntity>().AddAsync(row, cancellationToken);
        }

        row.Channel = channel.ToString();
        await database.SaveChangesAsync(cancellationToken);
        // The channel rides the per-mix chart dictionary, which caches for a fortnight.
        _cache.Remove(EFChartRepository.ChartCacheKey(mixId));
    }
}
