using Microsoft.EntityFrameworkCore;
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

        public EFOfficialLeaderboardRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            ILogger<EFOfficialLeaderboardRepository> logger)
        {
            _dbContext = factory.CreateDbContext();
            _logger = logger;
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

        public async Task<IEnumerable<string>> GetOfficialLeaderboardUsernames(CancellationToken cancellationToken)
        {
            return await _dbContext.UserOfficialLeaderboard.Select(e => e.Username).Distinct()
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
    }
}
