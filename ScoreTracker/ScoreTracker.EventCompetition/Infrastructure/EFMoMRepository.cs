using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.EventCompetition.Infrastructure.Entities;

namespace ScoreTracker.EventCompetition.Infrastructure;

internal sealed class EFMoMRepository : IMoMRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly IMemoryCache _memoryCache;

    public EFMoMRepository(IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache memoryCache)
    {
        _factory = factory;
        _memoryCache = memoryCache;
    }

    public async Task<MoMSeason?> GetSeason(int year, int quarter, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<MoMSeasonEntity>()
            .FirstOrDefaultAsync(s => s.Year == year && s.Quarter == (byte)quarter, cancellationToken);
        return entity == null
            ? null
            : new MoMSeason(entity.Id, entity.Year, entity.Quarter, entity.Name,
                entity.StartsAt, entity.EndsAt, entity.CreatedAt);
    }

    public async Task CreateSeason(MoMSeason season, IReadOnlyList<MoMBoardSeed> boards,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<MoMSeasonEntity>().AddAsync(new MoMSeasonEntity
        {
            Id = season.Id,
            Year = season.Year,
            Quarter = season.Quarter,
            Name = season.Name,
            StartsAt = season.StartsAt,
            EndsAt = season.EndsAt,
            CreatedAt = season.CreatedAt
        }, cancellationToken);

        foreach (var seed in boards)
        {
            await database.Set<MoMBoardEntity>().AddAsync(new MoMBoardEntity
            {
                Id = seed.Id,
                SeasonId = season.Id,
                MixId = MixIds.For(seed.Mix),
                ChartType = (byte)seed.ChartType,
                // The same wrapper shape the legacy Configuration column carried, so every
                // board — migrated or new — deserializes through one path.
                ScoringConfig = JsonSerializer.Serialize(
                    TournamentConfigurationJsonEntity.From(seed.Configuration))
            }, cancellationToken);

            // The snapshot is keyed per (season, mix); the seeds' chart-type pools are
            // disjoint, and the guard keeps a future same-mix overlap from doubling a row.
            foreach (var (chartId, level) in seed.SnapshotDeltas)
                if (!database.Set<MoMChartLevelEntity>().Local
                        .Any(l => l.SeasonId == season.Id &&
                                  l.MixId == MixIds.For(seed.Mix) && l.ChartId == chartId))
                    await database.Set<MoMChartLevelEntity>().AddAsync(new MoMChartLevelEntity
                    {
                        SeasonId = season.Id,
                        MixId = MixIds.For(seed.Mix),
                        ChartId = chartId,
                        Level = level
                    }, cancellationToken);
        }

        // The filtered unique (Year, Quarter) index makes a duplicate quarterly season
        // impossible (D2) — a concurrent create throws here rather than minting a twin.
        await database.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove(EFTournamentRepository.TourneyCacheKey);
    }

    public async Task PruneEndedEmptySeasons(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var seasonsWithSessions = database.Set<MoMSessionEntity>()
            .Join(database.Set<MoMBoardEntity>(), s => s.BoardId, b => b.Id, (s, b) => b.SeasonId)
            .Distinct();
        var pruned = await database.Set<MoMSeasonEntity>()
            .Where(s => s.EndsAt < now && !seasonsWithSessions.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);
        if (pruned > 0) _memoryCache.Remove(EFTournamentRepository.TourneyCacheKey);
    }
}
