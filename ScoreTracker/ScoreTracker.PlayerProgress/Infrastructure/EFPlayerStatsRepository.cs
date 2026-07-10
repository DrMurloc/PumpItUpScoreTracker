using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.PlayerProgress.Infrastructure
{
    internal sealed class EFPlayerStatsRepository : IPlayerStatsRepository,
        IPlayerStatsReader,
        IRequestHandler<GetPlayerStatsQuery, PlayerStatsRecord>
    {
        private readonly IMemoryCache _cache;
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

        public EFPlayerStatsRepository(IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache cache)
        {
            _cache = cache;
            _factory = factory;
        }

        private string CacheKey(MixEnum mix, Guid userId)
        {
            return $"{nameof(EFPlayerStatsRepository)}_PlayerStats_{mix}_{userId}";
        }

        public async Task<IEnumerable<Guid>> GetUserIdsWithStats(MixEnum mix, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            return await database.Set<PlayerStatsEntity>()
                .Where(p => p.MixId == mixId)
                .Select(p => p.UserId)
                .ToArrayAsync(cancellationToken);
        }

        public async Task SaveStats(MixEnum mix, Guid userId, PlayerStatsRecord newStats,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity = await database.Set<PlayerStatsEntity>()
                .FirstOrDefaultAsync(p => p.UserId == userId && p.MixId == mixId, cancellationToken);
            if (entity == null)
            {
                await database.AddAsync(new PlayerStatsEntity
                {
                    UserId = userId,
                    MixId = mixId,
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
            _cache.Remove(CacheKey(mix, userId));
        }

        public async Task<PlayerStatsRecord> GetStats(MixEnum mix, Guid userId, CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(CacheKey(mix, userId), async o =>
            {
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(30);

                var mixId = MixIds.For(mix);
                var entity =
                    await database.Set<PlayerStatsEntity>()
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.MixId == mixId, cancellationToken);
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

        public async Task<IEnumerable<PlayerStatsRecord>> GetStats(MixEnum mix, IEnumerable<Guid> userIds,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            return await database.Set<PlayerStatsEntity>()
                .Where(s => userIds.Contains(s.UserId) && s.MixId == mixId).Select(entity =>
                    new PlayerStatsRecord(entity.UserId, entity.TotalRating, entity.HighestLevel, entity.ClearCount,
                        entity.CoOpRating,
                        entity.AverageCoOpScore, entity.SkillRating, entity.AverageSkillScore, entity.AverageSkillLevel,
                        entity.SinglesRating,
                        entity.AverageSinglesScore, entity.AverageSinglesLevel, entity.DoublesRating,
                        entity.AverageDoublesScore, entity.AverageDoublesLevel, entity.CompetitiveLevel,
                        entity.SinglesCompetitiveLevel, entity.DoublesCompetitiveLevel)).ToArrayAsync(cancellationToken);
        }

        public async Task<IEnumerable<Guid>> GetPlayersByCompetitiveRange(MixEnum mix, ChartType? chartType,
            double competitiveLevel,
            double range,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var query = database.Set<PlayerStatsEntity>().Where(p => p.MixId == mixId);
            var min = competitiveLevel - range;
            var max = competitiveLevel + range;
            if (chartType == null)
                query = query.Where(p => p.CompetitiveLevel >= min && p.CompetitiveLevel <= max);
            else if (chartType == ChartType.Single)
                query = query.Where(p => p.SinglesCompetitiveLevel >= min && p.SinglesCompetitiveLevel <= max);
            else if (chartType == ChartType.Double)
                query = query.Where(p => p.DoublesCompetitiveLevel >= min && p.DoublesCompetitiveLevel <= max);

            return await query.Select(p => p.UserId).Distinct().ToArrayAsync(cancellationToken);
        }

        public async Task<PlayerStatsRecord> Handle(GetPlayerStatsQuery request, CancellationToken cancellationToken)
        {
            return await GetStats(request.Mix, request.UserId, cancellationToken);
        }

        public async Task DeleteStats(MixEnum mix, Guid userId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity = await database.Set<PlayerStatsEntity>()
                .FirstOrDefaultAsync(p => p.UserId == userId && p.MixId == mixId, cancellationToken);
            if (entity != null)
            {
                database.Set<PlayerStatsEntity>().Remove(entity);
                await database.SaveChangesAsync(cancellationToken);
            }

            _cache.Remove(CacheKey(mix, userId));
        }
    }
}
