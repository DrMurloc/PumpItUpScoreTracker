using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.OfficialMirror.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.OfficialMirror.Infrastructure
{
    internal sealed class EFOfficialLeaderboardRepository : IOfficialLeaderboardRepository
    {
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;

        public EFOfficialLeaderboardRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            ILogger<EFOfficialLeaderboardRepository> logger,
            IMemoryCache cache)
        {
            _factory = factory;
            _logger = logger;
            _cache = cache;
        }


        public async Task ClearLeaderboard(string leaderboardType, string leaderboardName,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            database.Set<UserOfficialLeaderboardEntity>().RemoveRange(
                database.Set<UserOfficialLeaderboardEntity>().Where(u =>
                    u.LeaderboardName == leaderboardName && u.LeaderboardType == leaderboardType));
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteEntry(MixEnum mix, UserOfficialLeaderboard entry, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            await database.Set<UserOfficialLeaderboardEntity>().AddAsync(new UserOfficialLeaderboardEntity
            {
                Id = Guid.NewGuid(),
                MixId = MixIds.For(mix),
                Place = entry.Place,
                LeaderboardName = entry.LeaderboardName,
                LeaderboardType = entry.OfficialLeaderboardType,
                Username = entry.Username,
                Score = entry.Score
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteEntries(MixEnum mix, IEnumerable<UserOfficialLeaderboard> entries,
            CancellationToken cancellationToken)
        {
            var array = entries.ToArray();
            if (array.Length == 0) return;
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            await database.Set<UserOfficialLeaderboardEntity>().AddRangeAsync(
                array.Select(entry => new UserOfficialLeaderboardEntity
                {
                    Id = Guid.NewGuid(),
                    MixId = mixId,
                    Place = entry.Place,
                    LeaderboardName = entry.LeaderboardName,
                    LeaderboardType = entry.OfficialLeaderboardType,
                    Username = entry.Username,
                    Score = entry.Score
                }), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<string>> GetOfficialLeaderboardUsernames(string? leaderboardType,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var result = database.Set<UserOfficialLeaderboardEntity>().AsQueryable();
            if (leaderboardType != null) result = result.Where(e => e.LeaderboardType == leaderboardType);
            return await result.Select(e => e.Username).Distinct()
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<UserOfficialLeaderboard>> GetOfficialLeaderboardStatuses(string username,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return await database.Set<UserOfficialLeaderboardEntity>().Where(e => e.Username == username)
                .Select(e =>
                    new UserOfficialLeaderboard(e.Username, e.Place, e.LeaderboardType, e.LeaderboardName, e.Score))
                .ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorldRankingRecord>> GetAllWorldRankings(CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return await database.Set<UserWorldRanking>()
                .Select(u => new WorldRankingRecord(u.UserName, u.Type, u.AverageLevel, u.AverageScore, u.SinglesCount,
                    u.DoublesCount, u.TotalRating, u.CompetitiveLevel, u.SinglesCompetitive, u.DoublesCompetitive))
                .ToArrayAsync(cancellationToken);
        }

        public async Task DeleteWorldRankings(CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            database.Set<UserWorldRanking>().RemoveRange(database.Set<UserWorldRanking>());
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveWorldRanking(MixEnum mix, WorldRankingRecord record,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            await database.Set<UserWorldRanking>().AddAsync(new UserWorldRanking
            {
                Id = Guid.NewGuid(),
                MixId = MixIds.For(mix),
                Type = record.Type,
                AverageLevel = record.AverageDifficulty,
                AverageScore = record.AverageScore,
                SinglesCount = record.SinglesCount,
                DoublesCount = record.DoublesCount,
                TotalRating = record.TotalRating,
                UserName = record.Username,
                CompetitiveLevel = record.CompetitiveLevel,
                SinglesCompetitive = record.SinglesCompetitiveLevel,
                DoublesCompetitive = record.DoublesCompetitiveLevel
            }, cancellationToken);

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task FixRankingOrders(CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var current = 1;
            var boards = await database.Set<UserOfficialLeaderboardEntity>().Select(u => u.LeaderboardName).Distinct()
                .ToArrayAsync(cancellationToken);
            var max = boards.Length;
            foreach (var boardName in boards)
            {
                _logger.LogInformation($"Board {current++}/{max}");
                var rankings = await database.Set<UserOfficialLeaderboardEntity>().Where(b => b.LeaderboardName == boardName)
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

                await database.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<IEnumerable<(string Username, Uri AvatarPath)>> GetUserAvatars(
            CancellationToken cancellationToken)
        {
            return (await GetAvatars(cancellationToken)).Select(kv => (kv.Key, kv.Value)).ToArray();
        }

        public async Task UpdateAllAvatarPaths(Uri oldPath, Uri newPath, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var oldPathString = oldPath.ToString();
            var entities = await database.Set<OfficialUserAvatarEntity>().Where(a => a.AvatarUrl == oldPathString)
                .ToArrayAsync(cancellationToken);
            foreach (var entity in entities) entity.AvatarUrl = newPath.ToString();

            await database.SaveChangesAsync(cancellationToken);
            _cache.Remove(AvatarCacheKey);
        }

        private const string AvatarCacheKey = $"{nameof(EFOfficialLeaderboardRepository)}__Avatars";

        private async Task<IDictionary<string, Uri>> GetAvatars(CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(AvatarCacheKey, async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(1);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                return (await database.Set<OfficialUserAvatarEntity>().ToArrayAsync(cancellationToken))
                    .ToDictionary(u => u.UserName, u => new Uri(u.AvatarUrl, UriKind.Absolute),
                        StringComparer.OrdinalIgnoreCase);
            });
        }

        public async Task SaveAvatar(string username, Uri avatarPath, CancellationToken cancellationToken)
        {
            var dict = await GetAvatars(cancellationToken);
            if (dict.TryGetValue(username, out var existing) && existing == avatarPath) return;

            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await
                database.Set<OfficialUserAvatarEntity>().FirstOrDefaultAsync(u => u.UserName == username, cancellationToken);
            if (entity == null)
                await database.Set<OfficialUserAvatarEntity>().AddAsync(new OfficialUserAvatarEntity
                {
                    Id = Guid.NewGuid(),
                    AvatarUrl = avatarPath.ToString(),
                    UserName = username
                }, cancellationToken);
            else
                entity.AvatarUrl = avatarPath.ToString();
            dict[username] = avatarPath;
            _cache.Set(AvatarCacheKey, dict, DateTimeOffset.Now + TimeSpan.FromHours(1));
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<DateTimeOffset?> GetLastImportTimestamp(MixEnum mix, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity = await database.Set<OfficialLeaderboardImportStateEntity>()
                .FirstOrDefaultAsync(e => e.MixId == mixId, cancellationToken);
            return entity?.LastImportedAt;
        }

        public async Task SetLastImportTimestamp(MixEnum mix, DateTimeOffset timestamp,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity = await database.Set<OfficialLeaderboardImportStateEntity>()
                .FirstOrDefaultAsync(e => e.MixId == mixId, cancellationToken);
            if (entity == null)
                await database.Set<OfficialLeaderboardImportStateEntity>().AddAsync(
                    new OfficialLeaderboardImportStateEntity { MixId = mixId, LastImportedAt = timestamp },
                    cancellationToken);
            else
                entity.LastImportedAt = timestamp;
            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
