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
///     migration, so there is nothing to invalidate, and 412 rows is small enough to hold —
///     the same shape as <see cref="EFArchivedSkillTagRepository" />.
/// </summary>
internal sealed class EFAvatarRepository : IAvatarRepository
{
    private const string CacheKey = "AvatarCatalog";

    /// <summary>
    ///     Which mix supplies a picture's canonical url when several list it. Phoenix 2 first
    ///     because its art carries no decorative frame and its catalog is the most complete.
    /// </summary>
    private static readonly MixEnum[] CanonicalMixOrder = { MixEnum.Phoenix2, MixEnum.Phoenix, MixEnum.XX };

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
                .ThenBy(a => a.PictureId)
                .ThenBy(a => a.Id)
                .Select(a => new { a.GroupId, a.PictureId, a.Name, a.ImageUrl, a.Mixes })
                .ToArrayAsync(cancellationToken);

            // Grouped after materializing: it is 412 rows already in memory, and doing it in SQL
            // would need a second pass to keep each level's order.
            IReadOnlyList<AvatarRecord> avatars = rows
                .GroupBy(a => a.GroupId)
                .Select(avatar =>
                {
                    var pictures = avatar
                        .GroupBy(a => a.PictureId)
                        .Select(picture =>
                        {
                            var listings = picture
                                .Select(a => new { Url = new Uri(a.ImageUrl, UriKind.Absolute), Mixes = Decode(a.Mixes) })
                                .ToArray();
                            var canonical = listings
                                .OrderBy(l => Array.IndexOf(CanonicalMixOrder, l.Mixes.FirstOrDefault()))
                                .First();
                            return new AvatarPictureRecord(
                                canonical.Url,
                                listings.SelectMany(l => l.Mixes).Distinct().OrderBy(m => m).ToArray(),
                                listings.Select(l => l.Url).ToArray());
                        })
                        .ToArray();
                    return new AvatarRecord(
                        avatar.First().Name,
                        // An avatar is available wherever ANY of its pictures is drawn.
                        pictures.SelectMany(p => p.Mixes).Distinct().OrderBy(m => m).ToArray(),
                        pictures);
                })
                .ToArray();
            return avatars;
        }))!;
    }

    /// <summary>
    ///     Unpacks the <c>1 &lt;&lt; (int)MixEnum</c> mask a row carries. Values the running enum
    ///     does not define are skipped rather than thrown on, so a mask written by a newer deploy
    ///     cannot take the whole picker down.
    /// </summary>
    private static IReadOnlyList<MixEnum> Decode(int mask)
    {
        return Enum.GetValues<MixEnum>()
            .Where(m => (mask & (1 << (int)m)) != 0)
            .OrderBy(m => m)
            .ToArray();
    }
}
