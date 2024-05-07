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
    public sealed class EFBountyRepository : IChartBountyRepository
    {
        //Will  need to refactor this if I ever support non prod environments
        //Mostly saving some tedious joins for now.
        private static readonly IDictionary<MixEnum, Guid> MixGuids = new Dictionary<MixEnum, Guid>
        {
            { MixEnum.XX, Guid.Parse("20F8CCF8-94B1-418D-B923-C375B042BDA8") },
            { MixEnum.Phoenix, Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B") }
        };

        private readonly IDbContextFactory<ChartAttemptDbContext> _dbContextFactory;
        private readonly IMemoryCache _cache;

        public EFBountyRepository(IDbContextFactory<ChartAttemptDbContext> dbContextFactory, IMemoryCache cache)
        {
            _dbContextFactory = dbContextFactory;
            _cache = cache;
        }

        private static string BountyCacheKey(ChartType chartType, DifficultyLevel level)
        {
            return $"{nameof(EFBountyRepository)}__Bounties__{chartType}__{level}";
        }


        public async Task<IEnumerable<ChartBounty>> GetChartBounties(ChartType chartType, DifficultyLevel level,
            CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(BountyCacheKey(chartType, level), async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(1);
                var mixId = MixGuids[MixEnum.Phoenix];
                var levelInt = (int)level;
                var chartTypeString = chartType.ToString();
                var database = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await (from cm in database.ChartMix
                        join c in database.Chart on cm.ChartId equals c.Id
                        join cb in database.ChartBounty on c.Id equals cb.ChartId
                        where cm.MixId == mixId && cm.Level == levelInt
                                                && c.Type == chartTypeString
                        select new ChartBounty(cb.ChartId, cb.Worth))
                    .ToArrayAsync(cancellationToken);
            }) ?? throw new ArgumentNullException("There was an issue when getting chart bounties from cache");
        }

        public async Task SetChartBounty(Guid chartId, int worth, CancellationToken cancellationToken)
        {
            var database = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await database.ChartBounty.FirstOrDefaultAsync(b => b.ChartId == chartId, cancellationToken);
            if (entity == null)
                await database.ChartBounty.AddAsync(new ChartBountyEntity
                {
                    ChartId = chartId,
                    Worth = worth
                }, cancellationToken);
            else
                entity.Worth = worth;

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task ClearMonthlyBoard(CancellationToken cancellationToken)
        {
            var database = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var allEntities = await database.BountyLeaderboard.ToArrayAsync(cancellationToken);
            foreach (var entity in allEntities) entity.MonthlyTotal = 0;
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task RedeemBounty(Guid userId, int worth, CancellationToken cancellationToken)
        {
            var database = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entity =
                await database.BountyLeaderboard.FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);
            if (entity == null)
            {
                await database.BountyLeaderboard.AddAsync(new BountyLeaaderboardEntity
                {
                    UserId = userId,
                    MonthlyTotal = worth,
                    Total = worth
                }, cancellationToken);
            }
            else
            {
                entity.Total += worth;
                entity.MonthlyTotal += worth;
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<BountyLeaderboard> GetBountyLeaderboard(Guid userId, CancellationToken cancellationToken)
        {
            var database = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await database.BountyLeaderboard.Where(l => l.UserId == userId)
                .Select(l => new BountyLeaderboard(l.UserId, l.MonthlyTotal, l.Total))
                .FirstOrDefaultAsync(cancellationToken) ?? new BountyLeaderboard(userId, 0, 0);
        }

        public async Task<IEnumerable<BountyLeaderboard>> GetBountyLeaderboard(CancellationToken cancellationToken)
        {
            var database = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await (from u in database.User
                    join l in database.BountyLeaderboard on u.Id equals l.UserId
                    where u.IsPublic
                    select new BountyLeaderboard(l.UserId, l.MonthlyTotal, l.Total))
                .ToArrayAsync(cancellationToken);
        }
    }
}
