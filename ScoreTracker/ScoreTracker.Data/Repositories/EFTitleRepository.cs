using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFTitleRepository : ITitleRepository
    {
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

        public EFTitleRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _factory = factory;
        }

        public async Task SaveTitles(Guid userId, IEnumerable<Name> acquiredTitles, CancellationToken cancellationToken)
        {
            var _dbContext = await _factory.CreateDbContextAsync(cancellationToken);
            var existingEntities =
                await _dbContext.UserTitle.Where(u => u.UserId == userId).ToArrayAsync(cancellationToken);
            var titleSet = acquiredTitles.Distinct().ToHashSet();
            var newEntities = titleSet.Where(t => existingEntities.All(e => e.Title != t))
                .Select(t => new UserTitleEntity
                {
                    Id = Guid.NewGuid(),
                    Title = t,
                    UserId = userId
                });
            var deleteEntities = existingEntities.Where(e => !titleSet.Contains(e.Title)).ToArray();

            _dbContext.UserTitle.RemoveRange(deleteEntities);
            await _dbContext.UserTitle.AddRangeAsync(newEntities, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task SetHighestDifficultyTitle(Guid userId, Name title, DifficultyLevel level,
            CancellationToken cancellationToken)
        {
            var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity =
                await database.UserHighestTitle.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (entity == null)
            {
                await database.UserHighestTitle.AddAsync(new UserHighestTitleEntity
                {
                    UserId = userId,
                    Level = level,
                    TitleName = title
                }, cancellationToken);
            }
            else
            {
                entity.TitleName = title;
                entity.Level = level;
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<Name>> GetCompletedTitles(Guid userId, CancellationToken cancellationToken)
        {
            var _dbContext = await _factory.CreateDbContextAsync(cancellationToken);
            return (await _dbContext.UserTitle.Where(u => u.UserId == userId).Select(u => u.Title)
                .ToArrayAsync(cancellationToken)).Select(Name.From).ToArray();
        }
    }
}
