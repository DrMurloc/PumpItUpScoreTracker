using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Application.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFPlayerStatsRepository : IPlayerStatsRepository,
        IRequestHandler<GetPlayerStatsQuery, PlayerStatsRecord>
    {
        private readonly IMemoryCache _cache;
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

        public EFPlayerStatsRepository(IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache cache)
        {
            _cache = cache;
            _factory = factory;
        }

        private string CacheKey(Guid userId)
        {
            return $"{nameof(EFPlayerStatsRepository)}_PlayerStats_{userId}";
        }

        public async Task SaveStats(Guid userId, PlayerStatsRecord newStats, CancellationToken cancellationToken)
        {
            var database = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await database.PlayerStats.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (entity == null)
            {
                await database.AddAsync(new PlayerStatsEntity
                {
                    UserId = userId,
                    CoOpRating = newStats.CoOpRating,
                    SinglesRating = newStats.SinglesRating,
                    DoublesRating = newStats.DoublesRating,
                    SkillRating = newStats.SkillRating,
                    TotalRating = newStats.TotalRating,
                    AverageCoOpScore = newStats.CoOpScore,
                    AverageDoublesLevel = newStats.DoublesLevel,
                    AverageDoublesScore = newStats.DoublesScore,
                    AverageSinglesLevel = newStats.SinglesLevel,
                    AverageSinglesScore = newStats.SinglesScore,
                    AverageSkillLevel = newStats.SkillLevel,
                    AverageSkillScore = newStats.SkillScore,
                    HighestLevel = newStats.HighestLevel,
                    ClearCount = newStats.ClearCount,
                    CompetitiveLevel = newStats.CompetitiveLevel,
                    SinglesCompetitiveLevel = newStats.SinglesCompetitiveLevel,
                    DoublesCompetitiveLevel = newStats.DoublesCompetitiveLevel
                }, cancellationToken);
            }
            else
            {
                entity.CoOpRating = newStats.CoOpRating;
                entity.SinglesRating = newStats.SinglesRating;
                entity.DoublesRating = newStats.DoublesRating;
                entity.SkillRating = newStats.SkillRating;
                entity.TotalRating = newStats.TotalRating;
                entity.AverageCoOpScore = newStats.CoOpScore;
                entity.AverageDoublesLevel = newStats.DoublesLevel;
                entity.AverageDoublesScore = newStats.DoublesScore;
                entity.AverageSinglesLevel = newStats.SinglesLevel;
                entity.AverageSinglesScore = newStats.SinglesScore;
                entity.AverageSkillLevel = newStats.SkillLevel;
                entity.AverageSkillScore = newStats.SkillScore;
                entity.HighestLevel = newStats.HighestLevel;
                entity.ClearCount = newStats.ClearCount;
                entity.CompetitiveLevel = newStats.CompetitiveLevel;
                entity.SinglesCompetitiveLevel = newStats.SinglesCompetitiveLevel;
                entity.DoublesCompetitiveLevel = newStats.DoublesCompetitiveLevel;
            }

            await database.SaveChangesAsync(cancellationToken);
            _cache.Remove(CacheKey(userId));
        }

        public async Task<PlayerStatsRecord> GetStats(Guid userId, CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(CacheKey(userId), async o =>
            {
                var database = await _factory.CreateDbContextAsync(cancellationToken);
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(5);

                var entity =
                    await database.PlayerStats.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
                if (entity == null)
                    return new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1);

                return new PlayerStatsRecord(entity.UserId, entity.TotalRating, entity.HighestLevel, entity.ClearCount,
                    entity.CoOpRating,
                    entity.AverageCoOpScore, entity.SkillRating, entity.AverageSkillScore, entity.AverageSkillLevel,
                    entity.SinglesRating,
                    entity.AverageSinglesScore, entity.AverageSinglesLevel, entity.DoublesRating,
                    entity.AverageDoublesScore, entity.AverageDoublesLevel, entity.CompetitiveLevel,
                    entity.SinglesCompetitiveLevel, entity.DoublesCompetitiveLevel);
            });
        }

        public async Task<IEnumerable<PlayerStatsRecord>> GetStats(IEnumerable<Guid> userIds,
            CancellationToken cancellationToken)
        {
            var database = await _factory.CreateDbContextAsync(cancellationToken);
            return await database.PlayerStats.Where(s => userIds.Contains(s.UserId)).Select(entity =>
                new PlayerStatsRecord(entity.UserId, entity.TotalRating, entity.HighestLevel, entity.ClearCount,
                    entity.CoOpRating,
                    entity.AverageCoOpScore, entity.SkillRating, entity.AverageSkillScore, entity.AverageSkillLevel,
                    entity.SinglesRating,
                    entity.AverageSinglesScore, entity.AverageSinglesLevel, entity.DoublesRating,
                    entity.AverageDoublesScore, entity.AverageDoublesLevel, entity.CompetitiveLevel,
                    entity.SinglesCompetitiveLevel, entity.DoublesCompetitiveLevel)).ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<Guid>> GetPlayersByCompetitiveRange(ChartType? chartType, double competitiveLevel,
            double range,
            CancellationToken cancellationToken)
        {
            var database = await _factory.CreateDbContextAsync(cancellationToken);
            var query = database.PlayerStats.AsQueryable();
            var min = competitiveLevel - range;
            var max = competitiveLevel + range;
            if (chartType == null)
                query = query.Where(p => p.CompetitiveLevel >= min && p.CompetitiveLevel <= max);
            else if (chartType == ChartType.Single)
                query = query.Where(p => p.DoublesCompetitiveLevel >= min && p.DoublesCompetitiveLevel <= max);
            else if (chartType == ChartType.Double)
                query = query.Where(p => p.SinglesCompetitiveLevel >= min && p.SinglesCompetitiveLevel <= max);

            return await query.Select(p => p.UserId).Distinct().ToArrayAsync(cancellationToken);
        }

        public async Task<PlayerStatsRecord> Handle(GetPlayerStatsQuery request, CancellationToken cancellationToken)
        {
            return await GetStats(request.UserId, cancellationToken);
        }
    }
}
