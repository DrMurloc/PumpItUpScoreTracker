using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Infrastructure
{
    internal sealed class EFTierListRepository : ITierListRepository
    {
        private readonly IMemoryCache _cache;
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
        private readonly IScoreReader _scores;
        private readonly ITitleRepository _titles;

        public EFTierListRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            IMemoryCache cache,
            IScoreReader scores,
            ITitleRepository titles)
        {
            _cache = cache;
            _factory = factory;
            _scores = scores;
            _titles = titles;
        }

        private static string TierListKey(Name tierListName)
        {
            return $"{nameof(EFTierListRepository)}_TierList_{tierListName}";
        }

        public async Task SaveEntry(SongTierListEntry entry, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var type = entry.TierListName.ToString();
            var entity = await database.Set<TierListEntryEntity>().Where(e => e.TierListName == type && e.ChartId == entry.ChartId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await database.Set<TierListEntryEntity>().AddAsync(new TierListEntryEntity
                {
                    Id = Guid.NewGuid(),
                    Category = entry.Category.ToString(),
                    ChartId = entry.ChartId,
                    TierListName = type,
                    Order = entry.Order
                });
            }
            else
            {
                entity.Category = entry.Category.ToString();
                entity.Order = entry.Order;
            }

            await database.SaveChangesAsync(cancellationToken);
            _cache.Remove(TierListKey(entry.TierListName));
        }

        public async Task<IEnumerable<Guid>> GetUsersOnLevel(DifficultyLevel level, CancellationToken cancellationToken,
            bool requireActive = false)
        {
            // Title cohorts come from PlayerProgress's ITitleRepository and activity from
            // the Ledger's IScoreReader — reads through published contracts, not joins onto
            // other verticals' tables (UserHighestTitle went PlayerProgress-internal at C50).
            var onLevel = await _titles.GetUserIdsOnHighestLevel(level, cancellationToken);
            if (!requireActive)
                return onLevel;

            var cutoff = DateTimeOffset.Now - TimeSpan.FromDays(120);
            var active = await _scores.GetActiveUserIds(cutoff, cancellationToken);
            return onLevel.Where(active.Contains).ToArray();
        }


        public async Task<IEnumerable<SongTierListEntry>> GetAllEntries(Name tierListName,
            CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(TierListKey(tierListName), async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(1);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                var nameString = tierListName.ToString();
                return (await database.Set<TierListEntryEntity>().ToArrayAsync(cancellationToken))
                    .Where(e => e.TierListName == nameString).Select(e =>
                        new SongTierListEntry(e.TierListName,
                            e.ChartId, Enum.Parse<TierListCategory>(e.Category), e.Order));
            });
        }

        public async Task SaveEntries(IEnumerable<SongTierListEntry> entries, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entryArray = entries.ToArray();
            var tierLists = entryArray.Select(e => e.TierListName.ToString()).Distinct().ToArray();
            var chartIds = entryArray.Select(e => e.ChartId).Distinct().ToArray();
            var entities = (await database.Set<TierListEntryEntity>()
                    .Where(e => tierLists.Contains(e.TierListName) && chartIds.Contains(e.ChartId))
                    .ToArrayAsync(cancellationToken))
                .GroupBy(e => e.TierListName)
                .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.ChartId));


            foreach (var entry in entryArray)
            {
                var entity = entities.TryGetValue(entry.TierListName, out var list)
                    ? list.TryGetValue(entry.ChartId, out var r) ? r : null
                    : null;
                if (entity == null)
                {
                    var type = entry.TierListName.ToString();
                    await database.Set<TierListEntryEntity>().AddAsync(new TierListEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Category = entry.Category.ToString(),
                        ChartId = entry.ChartId,
                        TierListName = type,
                        Order = entry.Order
                    }, cancellationToken);
                }
                else
                {
                    entity.Category = entry.Category.ToString();
                    entity.Order = entry.Order;
                }
            }


            await database.SaveChangesAsync(cancellationToken);

            foreach (var name in tierLists) _cache.Remove(TierListKey(name));
        }
    }
}
