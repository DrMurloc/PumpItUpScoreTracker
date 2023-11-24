using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFOfficialLeaderboardRepository : IOfficialLeaderboardRepository
    {
        private readonly ChartAttemptDbContext _dbContext;

        public EFOfficialLeaderboardRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _dbContext = factory.CreateDbContext();
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
                Username = entry.Username
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
                .Select(e => new UserOfficialLeaderboard(e.Username, e.Place, e.LeaderboardType, e.LeaderboardName))
                .ToArrayAsync(cancellationToken);
        }
    }
}
