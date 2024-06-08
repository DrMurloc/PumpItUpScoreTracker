using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFTierListRepository : ITierListRepository
    {
        private readonly ChartAttemptDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

        public EFTierListRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            IMemoryCache cache)
        {
            _dbContext = factory.CreateDbContext();
            _cache = cache;
            _factory = factory;
        }

        private static string TierListKey(Name tierListName)
        {
            return $"{nameof(EFTierListRepository)}_TierList_{tierListName}";
        }

        public async Task SaveEntry(SongTierListEntry entry, CancellationToken cancellationToken)
        {
            var type = entry.TierListName.ToString();
            var entity = await _dbContext.TierListEntry.Where(e => e.TierListName == type && e.ChartId == entry.ChartId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await _dbContext.TierListEntry.AddAsync(new TierListEntryEntity
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

            await _dbContext.SaveChangesAsync(cancellationToken);
            _cache.Remove(TierListKey(entry.TierListName));
        }

        public async Task<IEnumerable<Guid>> GetUsersOnLevel(DifficultyLevel level, CancellationToken cancellationToken)
        {
            var database = await _factory.CreateDbContextAsync(cancellationToken);
            return await database.UserHighestTitle.Where(e => e.Level == (int)level).Select(e => e.UserId)
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<SongTierListEntry>> GetAllEntries(Name tierListName,
            CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(TierListKey(tierListName), async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(1);
                var nameString = tierListName.ToString();
                return (await _dbContext.TierListEntry.ToArrayAsync(cancellationToken))
                    .Where(e => e.TierListName == nameString).Select(e =>
                        new SongTierListEntry(e.TierListName,
                            e.ChartId, Enum.Parse<TierListCategory>(e.Category), e.Order));
            });
        }
    }
}
