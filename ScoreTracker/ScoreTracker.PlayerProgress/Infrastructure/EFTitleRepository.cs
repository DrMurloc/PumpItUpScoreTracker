using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Infrastructure
{
    internal sealed class EFTitleRepository : ITitleRepository
    {
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
        private readonly IMemoryCache _cache;

        public EFTitleRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            IMemoryCache cache)
        {
            _factory = factory;
            _cache = cache;
        }

        private static string CacheKey(MixEnum mix)
        {
            return $"{nameof(EFTitleRepository)}__Titles__{mix}";
        }

        public async Task SaveTitles(MixEnum mix, Guid userId, IEnumerable<TitleAchievedRecord> acquiredTitles,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var existingEntities =
                await database.Set<UserTitleEntity>().Where(u => u.UserId == userId && u.MixId == mixId)
                    .ToArrayAsync(cancellationToken);
            var titleSet = acquiredTitles.Distinct().ToDictionary(t => t.Title);
            //Add
            var newEntities = titleSet.Where(t => existingEntities.All(e => e.Title != t.Key))
                .Select(t => new UserTitleEntity
                {
                    Id = Guid.NewGuid(),
                    Title = t.Key,
                    ParagonLevel = t.Value.ParagonLevel.ToString(),
                    UserId = userId,
                    MixId = mixId
                });
            //Update
            foreach (var entity in existingEntities.Where(e => titleSet.ContainsKey(e.Title)))
                entity.ParagonLevel = titleSet[entity.Title].ParagonLevel.ToString();
            //Delete
            var deleteEntities = existingEntities.Where(e => !titleSet.ContainsKey(e.Title)).ToArray();

            database.Set<UserTitleEntity>().RemoveRange(deleteEntities);
            await database.Set<UserTitleEntity>().AddRangeAsync(newEntities, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            _cache.Remove(CacheKey(mix));
            _cache.Remove(CacheKey(mix) + "__UserCount");
        }

        public async Task SetHighestDifficultyTitle(MixEnum mix, Guid userId, Name title, DifficultyLevel level,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity =
                await database.Set<UserHighestTitleEntity>()
                    .FirstOrDefaultAsync(u => u.UserId == userId && u.MixId == mixId, cancellationToken);
            if (entity == null)
            {
                await database.Set<UserHighestTitleEntity>().AddAsync(new UserHighestTitleEntity
                {
                    UserId = userId,
                    MixId = mixId,
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

        public async Task<IEnumerable<TitleAchievedRecord>> GetCompletedTitles(MixEnum mix, Guid userId,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            return (await database.Set<UserTitleEntity>().Where(u => u.UserId == userId && u.MixId == mixId)
                    .ToArrayAsync(cancellationToken))
                .Select(u => new TitleAchievedRecord(u.UserId, u.Title, Enum.Parse<ParagonLevel>(u.ParagonLevel)))
                .ToArray();
        }

        public async Task<DifficultyLevel> GetCurrentTitleLevel(MixEnum mix, Guid userId,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            return (await database.Set<UserHighestTitleEntity>()
                .Where(u => u.UserId == userId && u.MixId == mixId)
                .FirstOrDefaultAsync(cancellationToken))?.Level ?? 10;
        }

        public async Task<IEnumerable<TitleAggregationRecord>> GetTitleAggregations(MixEnum mix,
            CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(CacheKey(mix), async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                var mixId = MixIds.For(mix);
                return await (from u in database.User
                    where u.GameTag != null
                    join ut in database.Set<UserTitleEntity>() on u.Id equals ut.UserId
                    where ut.MixId == mixId
                    group ut by ut.Title
                    into g
                    select new TitleAggregationRecord(g.Key, g.Count())).ToArrayAsync(cancellationToken);
            });
        }

        public async Task<int> CountTitledUsers(CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync($"{nameof(EFTitleRepository)}__Titles__UserCount", async o =>
            {
                o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                return await database.User.Where(u => u.GameTag != null).CountAsync(cancellationToken);
            });
        }

        public async Task<IEnumerable<TitleAchievedRecord>> GetUsersWithTitle(MixEnum mix, Name title,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var titleString = title.ToString();
            var mixId = MixIds.For(mix);
            return await database.Set<UserTitleEntity>().Where(t => t.Title == titleString && t.MixId == mixId)
                .Select(u => new TitleAchievedRecord(u.UserId, title, Enum.Parse<ParagonLevel>(u.ParagonLevel)))
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<Guid>> GetUserIdsOnHighestLevel(MixEnum mix, DifficultyLevel level,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var levelInt = (int)level;
            var mixId = MixIds.For(mix);
            return await database.Set<UserHighestTitleEntity>().Where(e => e.Level == levelInt && e.MixId == mixId)
                .Select(e => e.UserId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
        }

        public async Task DeleteHighestTitle(MixEnum mix, Guid userId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity =
                await database.Set<UserHighestTitleEntity>()
                    .FirstOrDefaultAsync(u => u.UserId == userId && u.MixId == mixId, cancellationToken);
            if (entity != null)
            {
                database.Set<UserHighestTitleEntity>().Remove(entity);
                await database.SaveChangesAsync(cancellationToken);
            }

            _cache.Remove(CacheKey(mix));
            _cache.Remove(CacheKey(mix) + "__UserCount");
        }
    }
}
