using ScoreTracker.Domain.Models.Titles.Phoenix2;
﻿using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Caching;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.PlayerProgress.Infrastructure
{
    internal sealed class EFPlayerStatsRepository : IPlayerStatsRepository,
        IPlayerStatsReader,
        IRequestHandler<GetPlayerStatsQuery, PlayerStatsRecord>,
        IRequestHandler<GetPlayersStatsQuery, IEnumerable<PlayerStatsRecord>>
    {
        private readonly IMemoryCache _cache;
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

        public EFPlayerStatsRepository(IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache cache)
        {
            _cache = cache;
            _factory = factory;
        }

        // A Viewer key: the season's stats row is a different row, and this is where that lands.
        private static string CacheKey(MixEnum mix, Guid userId, SeasonId season)
        {
            return CacheKeys.Viewer(nameof(EFPlayerStatsRepository), mix, season, userId);
        }

        /// <summary>
        ///     The stats table as one season sees it: the AllTime filter's rows, or — that filter
        ///     dropped by name, never wholesale — the rows carrying <paramref name="season" />
        ///     (docs/design/seasons.md D12).
        /// </summary>
        private static IQueryable<PlayerStatsEntity> Stats(ChartAttemptDbContext database, SeasonId season)
        {
            if (season.IsAllTime) return database.Set<PlayerStatsEntity>();
            var value = season.Value;
            return database.Set<PlayerStatsEntity>().IgnoreQueryFilters([QueryFilters.AllTime])
                .Where(p => p.SeasonId == value);
        }

        public Task<IEnumerable<Guid>> GetUserIdsWithStats(MixEnum mix, CancellationToken cancellationToken)
        {
            return GetUserIdsWithStats(mix, SeasonId.AllTime, cancellationToken);
        }

        public async Task<IEnumerable<Guid>> GetUserIdsWithStats(MixEnum mix, SeasonId season,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            return await Stats(database, season)
                .Where(p => p.MixId == mixId)
                .Select(p => p.UserId)
                .ToArrayAsync(cancellationToken);
        }

        public Task SaveStats(MixEnum mix, Guid userId, PlayerStatsRecord newStats,
            CancellationToken cancellationToken)
        {
            return SaveStats(mix, userId, newStats, SeasonId.AllTime, cancellationToken);
        }

        public async Task SaveStats(MixEnum mix, Guid userId, PlayerStatsRecord newStats, SeasonId season,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity = await Stats(database, season)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.MixId == mixId, cancellationToken);
            if (entity == null)
            {
                await database.AddAsync(new PlayerStatsEntity
                {
                    UserId = userId,
                    MixId = mixId,
                    SeasonId = season.Value,
                    TotalPumbility = newStats.TotalPumbility,
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
                    DoublesCompetitiveLevel = newStats.DoublesCompetitiveLevel,
                    EstimatedPumbilityRank = newStats.EstimatedPumbilityRank,
                    EstimatedSinglesPumbilityRank = newStats.EstimatedSinglesPumbilityRank,
                    EstimatedDoublesPumbilityRank = newStats.EstimatedDoublesPumbilityRank,
                    PumbilityBoardAsOf = newStats.PumbilityBoardAsOf
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
                entity.EstimatedPumbilityRank = newStats.EstimatedPumbilityRank;
                entity.EstimatedSinglesPumbilityRank = newStats.EstimatedSinglesPumbilityRank;
                entity.EstimatedDoublesPumbilityRank = newStats.EstimatedDoublesPumbilityRank;
                entity.PumbilityBoardAsOf = newStats.PumbilityBoardAsOf;
                entity.TotalPumbility = newStats.TotalPumbility;
            }

            await database.SaveChangesAsync(cancellationToken);
            _cache.Remove(CacheKey(mix, userId, season));
        }

        public Task<PlayerStatsRecord> GetStats(MixEnum mix, Guid userId, CancellationToken cancellationToken)
        {
            return GetStats(mix, userId, SeasonId.AllTime, cancellationToken);
        }

        public async Task<PlayerStatsRecord> GetStats(MixEnum mix, Guid userId, SeasonId season,
            CancellationToken cancellationToken)
        {
            return await _cache.GetOrCreateAsync(CacheKey(mix, userId, season), async o =>
            {
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                // A season's entry is short-lived instead of evicted: the wipe cannot enumerate
                // seasons from here, and the nightly rollup rewrites every season row anyway.
                o.AbsoluteExpiration = DateTimeOffset.Now +
                                       (season.IsAllTime ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(5));

                var mixId = MixIds.For(mix);
                var entity =
                    await Stats(database, season)
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.MixId == mixId, cancellationToken);
                if (entity == null)
                    return new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1);

                return new PlayerStatsRecord(entity.UserId, entity.TotalRating, entity.HighestLevel, entity.ClearCount,
                    entity.CoOpRating,
                    entity.AverageCoOpScore, entity.SkillRating, entity.AverageSkillScore, entity.AverageSkillLevel,
                    entity.SinglesRating,
                    entity.AverageSinglesScore, entity.AverageSinglesLevel, entity.DoublesRating,
                    entity.AverageDoublesScore, entity.AverageDoublesLevel, entity.CompetitiveLevel,
                    entity.SinglesCompetitiveLevel, entity.DoublesCompetitiveLevel,
                    entity.EstimatedPumbilityRank, entity.EstimatedSinglesPumbilityRank,
                    entity.EstimatedDoublesPumbilityRank, entity.PumbilityBoardAsOf, entity.TotalPumbility);
            });
        }

        public Task<IEnumerable<PlayerStatsRecord>> GetStats(MixEnum mix, IEnumerable<Guid> userIds,
            CancellationToken cancellationToken)
        {
            return GetStats(mix, userIds, SeasonId.AllTime, cancellationToken);
        }

        public async Task<IEnumerable<PlayerStatsRecord>> GetStats(MixEnum mix, IEnumerable<Guid> userIds,
            SeasonId season, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            return await Stats(database, season)
                .Where(s => userIds.Contains(s.UserId) && s.MixId == mixId).Select(entity =>
                    new PlayerStatsRecord(entity.UserId, entity.TotalRating, entity.HighestLevel, entity.ClearCount,
                        entity.CoOpRating,
                        entity.AverageCoOpScore, entity.SkillRating, entity.AverageSkillScore, entity.AverageSkillLevel,
                        entity.SinglesRating,
                        entity.AverageSinglesScore, entity.AverageSinglesLevel, entity.DoublesRating,
                        entity.AverageDoublesScore, entity.AverageDoublesLevel, entity.CompetitiveLevel,
                        entity.SinglesCompetitiveLevel, entity.DoublesCompetitiveLevel,
                        entity.EstimatedPumbilityRank, entity.EstimatedSinglesPumbilityRank,
                        entity.EstimatedDoublesPumbilityRank, entity.PumbilityBoardAsOf, entity.TotalPumbility))
                .ToArrayAsync(cancellationToken);
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

        public async Task<IEnumerable<Guid>> GetPlayersByPoolOfType(MixEnum mix, ChartType chartType,
            double minimumPool, double maximumPool, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            // SinglesRating and DoublesRating ARE the per-type pools — each type's top fifty summed,
            // stored unrounded (docs/UX-GUIDELINES.md, the PUMBILITY precision rule) — and
            // SkillRating the merged one. The window is inclusive at both ends (D53).
            var query = database.Set<PlayerStatsEntity>().Where(p => p.MixId == mixId);
            query = chartType switch
            {
                ChartType.Single => query.Where(p => p.SinglesRating >= minimumPool && p.SinglesRating <= maximumPool),
                ChartType.Double => query.Where(p => p.DoublesRating >= minimumPool && p.DoublesRating <= maximumPool),
                _ => query.Where(p => p.SkillRating >= minimumPool && p.SkillRating <= maximumPool)
            };
            return await query.Select(p => p.UserId).Distinct().ToArrayAsync(cancellationToken);
        }

        /// <summary>
        ///     One band of a ladder (D68). Half-open on purpose: the peer window above is a distance
        ///     from a pool and takes both ends, while a band ends where the next rung's title begins.
        /// </summary>
        public async Task<IEnumerable<Guid>> GetPlayersInPoolBand(MixEnum mix, PumbilityPool pool, double floor,
            double? ceiling, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var top = ceiling ?? double.MaxValue;
            var query = database.Set<PlayerStatsEntity>().Where(p => p.MixId == mixId);
            query = pool switch
            {
                PumbilityPool.Singles => query.Where(p => p.SinglesRating >= floor && p.SinglesRating < top),
                PumbilityPool.Doubles => query.Where(p => p.DoublesRating >= floor && p.DoublesRating < top),
                _ => query.Where(p => p.SkillRating >= floor && p.SkillRating < top)
            };
            return await query.Select(p => p.UserId).Distinct().ToArrayAsync(cancellationToken);
        }

        public async Task<PlayerStatsRecord> Handle(GetPlayerStatsQuery request, CancellationToken cancellationToken)
        {
            return await GetStats(request.Mix, request.UserId, cancellationToken);
        }

        public async Task<IEnumerable<PlayerStatsRecord>> Handle(GetPlayersStatsQuery request,
            CancellationToken cancellationToken)
        {
            return await GetStats(request.Mix, request.UserIds, cancellationToken);
        }

        public async Task DeleteStats(MixEnum mix, Guid userId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            // The mix wipe crosses seasons (docs/design/seasons.md D12): every filter dropped, so a
            // player's season rows go with the all-time one they were computed beside. Only the
            // all-time cache entry is addressable from here; a season's runs on a short TTL.
            await database.Set<PlayerStatsEntity>().IgnoreQueryFilters()
                .Where(p => p.UserId == userId && p.MixId == mixId)
                .ExecuteDeleteAsync(cancellationToken);

            _cache.Remove(CacheKey(mix, userId, SeasonId.AllTime));
        }
    }
}
