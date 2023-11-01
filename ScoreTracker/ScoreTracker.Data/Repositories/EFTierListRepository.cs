using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public EFTierListRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _dbContext = factory.CreateDbContext();
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
        }

        public async Task<IEnumerable<SongTierListEntry>> GetAllEntries(Name tierListName,
            CancellationToken cancellationToken)
        {
            var nameString = tierListName.ToString();
            return (await _dbContext.TierListEntry.ToArrayAsync(cancellationToken))
                .Where(e => e.TierListName == nameString).Select(e =>
                    new SongTierListEntry(e.TierListName,
                        e.ChartId, Enum.Parse<TierListCategory>(e.Category), e.Order));
        }
    }
}
