using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFOfficialLeaderboardRepository : IOfficialLeaderboardRepository
    {
        private readonly ChartAttemptDbContext _dbContext;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;

        public EFOfficialLeaderboardRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            ILogger<EFOfficialLeaderboardRepository> logger,
            IMemoryCache cache)
        {
            _dbContext = factory.CreateDbContext();
            _logger = logger;
            _cache = cache;
        }


        public async Task ClearLeaderboard(string leaderboardType, string leaderboardName,
            CancellationToken cancellationToken)
        {
            _dbContext.UserOfficialLeaderboard.RemoveRange(
                _dbContext.UserOfficialLeaderboard.Where(u =>
                    u.LeaderboardName == leaderboardName && u.LeaderboardType == leaderboardType));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteEntry(UserOfficialLeaderboard entry, CancellationToken cancellationToken)
        {
            await _dbContext.UserOfficialLeaderboard.AddAsync(new UserOfficialLeaderboardEntity
            {
                Id = Guid.NewGuid(),
                Place = entry.Place,
                LeaderboardName = entry.LeaderboardName,
                LeaderboardType = entry.OfficialLeaderboardType,
                Username = entry.Username,
                Score = entry.Score
            }, cancellationToken);
        }

        public async Task<IEnumerable<string>> GetOfficialLeaderboardUsernames(string? leaderboardType,
            CancellationToken cancellationToken)
        {
            var result = _dbContext.UserOfficialLeaderboard.AsQueryable();
            if (leaderboardType != null) result = result.Where(e => e.LeaderboardType == leaderboardType);
            return await result.Select(e => e.Username).Distinct()
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<UserOfficialLeaderboard>> GetOfficialLeaderboardStatuses(string username,
            CancellationToken cancellationToken)
        {
            return await _dbContext.UserOfficialLeaderboard.Where(e => e.Username == username)
                .Select(e =>
                    new UserOfficialLeaderboard(e.Username, e.Place, e.LeaderboardType, e.LeaderboardName, e.Score))
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorldRankingRecord>> GetAllWorldRankings(CancellationToken cancellationToken)
        {
            return await _dbContext.UserWorldRanking
                .Select(u => new WorldRankingRecord(u.UserName, u.Type, u.AverageLevel, u.AverageScore, u.SinglesCount,
                    u.DoublesCount, u.TotalRating)).ToArrayAsync(cancellationToken);
        }

        public async Task DeleteWorldRankings(CancellationToken cancellationToken)
        {
            _dbContext.UserWorldRanking.RemoveRange(_dbContext.UserWorldRanking);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveWorldRanking(WorldRankingRecord record, CancellationToken cancellationToken)
        {
            await _dbContext.UserWorldRanking.AddAsync(new UserWorldRanking
            {
                Id = Guid.NewGuid(),
                Type = record.Type,
                AverageLevel = record.AverageDifficulty,
                AverageScore = record.AverageScore,
                SinglesCount = record.SinglesCount,
                DoublesCount = record.DoublesCount,
                TotalRating = record.TotalRating,
                UserName = record.Username
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task FixRankingOrders(CancellationToken cancellationToken)
        {
            var current = 1;
            var boards = await _dbContext.UserOfficialLeaderboard.Select(u => u.LeaderboardName).Distinct()
                .ToArrayAsync(cancellationToken);
            var max = boards.Length;
            foreach (var boardName in boards)
            {
                _logger.LogInformation($"Board {current++}/{max}");
                var rankings = await _dbContext.UserOfficialLeaderboard.Where(b => b.LeaderboardName == boardName)
                    .ToArrayAsync(cancellationToken);
                var rollingPlace = 1;
                foreach (var rankingGroup in rankings.GroupBy(r => r.Score).OrderByDescending(g => g.Key))
                {
                    var currentPlace = rollingPlace;
                    foreach (var entity in rankingGroup)
                    {
                        rollingPlace++;
                        entity.Place = currentPlace;
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<IEnumerable<(string Username, Uri AvatarPath)>> GetUserAvatars(
            CancellationToken cancellationToken)
        {
            return (await GetAvatars(cancellationToken)).Select(kv => (kv.Key, kv.Value)).ToArray();
        }

        public async Task UpdateAllAvatarPaths(Uri oldPath, Uri newPath, CancellationToken cancellationToken)
        {
            var oldPathString = oldPath.ToString();
            var entities = await _dbContext.OfficialUserAvatar.Where(a => a.AvatarUrl == oldPathString)
                .ToArrayAsync(cancellationToken);
            foreach (var entity in entities) entity.AvatarUrl = newPath.ToString();

            await _dbContext.SaveChangesAsync(cancellationToken);
            _cache.Remove(AvatarCacheKey);
        }

        private const string AvatarCacheKey = $"{nameof(EFOfficialLeaderboardRepository)}__Avatars";

        private async Task<IDictionary<string, Uri>> GetAvatars(CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(AvatarCacheKey, async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(1);
                return (await _dbContext.OfficialUserAvatar.ToArrayAsync(cancellationToken))
                    .ToDictionary(u => u.UserName, u => new Uri(u.AvatarUrl, UriKind.Absolute),
                        StringComparer.OrdinalIgnoreCase);
            });
        }

        public async Task SaveAvatar(string username, Uri avatarPath, CancellationToken cancellationToken)
        {
            var dict = await GetAvatars(cancellationToken);
            if (dict.TryGetValue(username, out var existing) && existing == avatarPath) return;

            var entity = await
                _dbContext.OfficialUserAvatar.FirstOrDefaultAsync(u => u.UserName == username, cancellationToken);
            if (entity == null)
                await _dbContext.OfficialUserAvatar.AddAsync(new OfficialUserAvatarEntity
                {
                    Id = Guid.NewGuid(),
                    AvatarUrl = avatarPath.ToString(),
                    UserName = username
                }, cancellationToken);
            else
                entity.AvatarUrl = avatarPath.ToString();
            dict[username] = avatarPath;
            _cache.Set(AvatarCacheKey, dict, DateTimeOffset.Now + TimeSpan.FromHours(1));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
