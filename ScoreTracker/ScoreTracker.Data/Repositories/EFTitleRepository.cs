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
    public sealed class EFTitleRepository : ITitleRepository
    {
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
        private readonly IMemoryCache _cache;

        public EFTitleRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            IMemoryCache cache)
        {
            _factory = factory;
            _cache = cache;
        }

        private readonly string CacheKey = $"{nameof(EFTitleRepository)}__Titles";

        public async Task SaveTitles(Guid userId, IEnumerable<TitleAchievedRecord> acquiredTitles,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var existingEntities =
                await database.UserTitle.Where(u => u.UserId == userId).ToArrayAsync(cancellationToken);
            var titleSet = acquiredTitles.Distinct().ToDictionary(t => t.Title);
            //Add
            var newEntities = titleSet.Where(t => existingEntities.All(e => e.Title != t.Key))
                .Select(t => new UserTitleEntity
                {
                    Id = Guid.NewGuid(),
                    Title = t.Key,
                    ParagonLevel = t.Value.ParagonLevel.ToString(),
                    UserId = userId
                });
            //Update
            foreach (var entity in existingEntities.Where(e => titleSet.ContainsKey(e.Title)))
                entity.ParagonLevel = titleSet[entity.Title].ParagonLevel.ToString();
            //Delete
            var deleteEntities = existingEntities.Where(e => !titleSet.ContainsKey(e.Title)).ToArray();

            database.UserTitle.RemoveRange(deleteEntities);
            await database.UserTitle.AddRangeAsync(newEntities, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            _cache.Remove(CacheKey);
            _cache.Remove(CacheKey + "__UserCount");
        }

        public async Task SetHighestDifficultyTitle(Guid userId, Name title, DifficultyLevel level,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
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

        public async Task<IEnumerable<TitleAchievedRecord>> GetCompletedTitles(Guid userId,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return (await database.UserTitle.Where(u => u.UserId == userId).ToArrayAsync(cancellationToken))
                .Select(u => new TitleAchievedRecord(u.UserId, u.Title, Enum.Parse<ParagonLevel>(u.ParagonLevel)))
                .ToArray();
        }

        public async Task<DifficultyLevel> GetCurrentTitleLevel(Guid userId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return (await database.UserHighestTitle
                .Where(u => u.UserId == userId).FirstOrDefaultAsync(cancellationToken))?.Level ?? 10;
        }

        public async Task<IEnumerable<TitleAggregationRecord>> GetTitleAggregations(CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(CacheKey, async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                return await (from u in database.User
                    where u.GameTag != null
                    join ut in database.UserTitle on u.Id equals ut.UserId
                    group ut by ut.Title
                    into g
                    select new TitleAggregationRecord(g.Key, g.Count())).ToArrayAsync(cancellationToken);
            });
        }

        public async Task<int> CountTitledUsers(CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(CacheKey + "__UserCount", async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                return await database.User.Where(u => u.GameTag != null).CountAsync(cancellationToken);
            });
        }

        public async Task<IEnumerable<TitleAchievedRecord>> GetUsersWithTitle(Name title,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var titleString = title.ToString();
            return await database.UserTitle.Where(t => t.Title == titleString).Select(u =>
                    new TitleAchievedRecord(u.UserId, title, Enum.Parse<ParagonLevel>(u.ParagonLevel)))
                .ToArrayAsync(cancellationToken);
        }

        public async Task DeleteHighestTitle(Guid userId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity =
                await database.UserHighestTitle.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (entity != null)
            {
                database.UserHighestTitle.Remove(entity);
                await database.SaveChangesAsync(cancellationToken);
            }

            _cache.Remove(CacheKey);
            _cache.Remove(CacheKey + "__UserCount");
        }
    }
}
