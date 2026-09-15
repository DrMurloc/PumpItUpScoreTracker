using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Seasons.Domain;
using ScoreTracker.Seasons.Infrastructure.Entities;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Seasons.Infrastructure;

/// <summary>
///     One EF adapter serving the vertical's write port and the published reader (the
///     EFDailyStepRepository shape). The whole table is a row per quarter, so it is read once into a
///     short-lived cache and every question — which season holds this play, is it sealed — is
///     answered in memory; the writer asks it once per imported score.
/// </summary>
internal sealed class EFSeasonRepository(IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache cache)
    : ISeasonRepository, ISeasonReader
{
    // Names no mix, so it is spelled here rather than through CacheKeys; the roll evicts it on
    // every write and the TTL is the backstop for a second host.
    private const string AllKey = $"{nameof(EFSeasonRepository)}__All";

    public async Task<IReadOnlyList<SeasonRecord>> GetAll(CancellationToken cancellationToken)
    {
        var seasons = await cache.GetOrCreateAsync(AllKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var rows = await database.Set<SeasonEntity>()
                .OrderByDescending(s => s.Id)
                .ToArrayAsync(cancellationToken);
            return rows.Select(ToRecord).ToArray();
        });
        return seasons ?? [];
    }

    public async Task Add(SeasonRecord season, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        database.Set<SeasonEntity>().Add(new SeasonEntity
        {
            Id = season.Id.Value,
            Name = season.Name,
            StartsAt = season.StartsAt,
            EndsAt = season.EndsAt,
            SealedAt = season.SealedAt,
            IsBalanced = season.IsBalanced
        });
        await database.SaveChangesAsync(cancellationToken);
        cache.Remove(AllKey);
    }

    public async Task Seal(SeasonId season, DateTimeOffset sealedAt, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var id = season.Value;
        await database.Set<SeasonEntity>()
            .Where(s => s.Id == id && s.SealedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.SealedAt, sealedAt), cancellationToken);
        cache.Remove(AllKey);
    }

    Task<IReadOnlyList<SeasonRecord>> ISeasonReader.GetSeasons(CancellationToken cancellationToken)
    {
        return GetAll(cancellationToken);
    }

    async Task<SeasonRecord?> ISeasonReader.GetSeasonAt(DateTimeOffset at, CancellationToken cancellationToken)
    {
        return (await GetAll(cancellationToken)).FirstOrDefault(s => s.Holds(at));
    }

    private static SeasonRecord ToRecord(SeasonEntity e)
    {
        return new SeasonRecord(SeasonId.From(e.Id), e.Name, e.StartsAt, e.EndsAt, e.SealedAt, e.IsBalanced);
    }
}
