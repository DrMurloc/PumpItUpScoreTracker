using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Infrastructure;

/// <summary>
///     The avatar catalog, cached whole and forever. Nothing writes the table after its seed
///     migration, so there is nothing to invalidate, and 182 rows is small enough to hold —
///     the same shape as <see cref="EFArchivedSkillTagRepository" />.
/// </summary>
internal sealed class EFAvatarRepository : IAvatarRepository
{
    private const string CacheKey = "AvatarCatalog";

    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAvatarRepository(IMemoryCache cache, IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _cache = cache;
        _factory = factory;
    }

    public async Task<IReadOnlyList<AvatarRecord>> GetAvatars(CancellationToken cancellationToken = default)
    {
        return (await _cache.GetOrCreateAsync(CacheKey, async _ =>
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var rows = await database.Set<AvatarEntity>()
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Id)
                .Select(a => new { a.GroupId, a.Name, a.ImageUrl, a.Mixes })
                .ToArrayAsync(cancellationToken);

            // GroupBy after materializing: the grouping is over 182 rows already in memory, and
            // doing it in SQL would need a second pass to keep each group's picture order.
            IReadOnlyList<AvatarRecord> avatars = rows
                .GroupBy(a => a.GroupId)
                .Select(g =>
                {
                    var pictures = g
                        .Select(a => new AvatarPictureRecord(new Uri(a.ImageUrl, UriKind.Absolute), Decode(a.Mixes)))
                        .ToArray();
                    return new AvatarRecord(
                        g.First().Name,
                        // An avatar is available wherever ANY of its pictures is drawn.
                        pictures.SelectMany(p => p.Mixes).Distinct().OrderBy(m => m).ToArray(),
                        pictures);
                })
                .ToArray();
            return avatars;
        }))!;
    }

    /// <summary>
    ///     Unpacks the <c>1 &lt;&lt; (int)MixEnum</c> bitmask the seed writes. Values the running
    ///     enum does not define are skipped rather than thrown on, so a mask written by a newer
    ///     deploy cannot take the whole picker down.
    /// </summary>
    private static IReadOnlyList<MixEnum> Decode(int mask)
    {
        return Enum.GetValues<MixEnum>()
            .Where(m => (mask & (1 << (int)m)) != 0)
            .OrderBy(m => m)
            .ToArray();
    }
}
