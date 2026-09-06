using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

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

        AddSeeds(database, season.Id, boards, new HashSet<(Guid, Guid)>());

        // The filtered unique (Year, Quarter) index makes a duplicate quarterly season
        // impossible (D2) — a concurrent create throws here rather than minting a twin.
        await database.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove(EFTournamentRepository.TourneyCacheKey);
    }

    public async Task<IReadOnlyList<MoMBoardKey>> GetBoardKeys(Guid seasonId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var boards = await database.Set<MoMBoardEntity>()
            .Where(b => b.SeasonId == seasonId)
            .Select(b => new { b.MixId, b.ChartType })
            .ToArrayAsync(cancellationToken);
        return boards.Select(b => new MoMBoardKey(MixIds.ToEnum(b.MixId), (ChartType)b.ChartType)).ToArray();
    }

    public async Task AddBoards(Guid seasonId, IReadOnlyList<MoMBoardSeed> boards, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // The snapshot rows the season already holds for these mixes: a heal adds a board's
        // deltas beside them and never doubles one (the key is season + mix + chart).
        var mixIds = boards.Select(b => MixIds.For(b.Mix)).Distinct().ToArray();
        var held = await database.Set<MoMChartLevelEntity>()
            .Where(l => l.SeasonId == seasonId && mixIds.Contains(l.MixId))
            .Select(l => new { l.MixId, l.ChartId })
            .ToArrayAsync(cancellationToken);
        AddSeeds(database, seasonId, boards, held.Select(h => (h.MixId, h.ChartId)).ToHashSet());
        await database.SaveChangesAsync(cancellationToken);
        _memoryCache.Remove(EFTournamentRepository.TourneyCacheKey);
    }

    /// <summary>
    ///     Stages each seed's board row and its snapshot delta rows. The snapshot is keyed per
    ///     (season, mix, chart); <paramref name="taken" /> carries the rows that already exist or
    ///     were staged earlier in this call, so two seeds of one mix cannot double a row.
    /// </summary>
    private static void AddSeeds(ChartAttemptDbContext database, Guid seasonId,
        IEnumerable<MoMBoardSeed> boards, HashSet<(Guid MixId, Guid ChartId)> taken)
    {
        foreach (var seed in boards)
        {
            var mixId = MixIds.For(seed.Mix);
            database.Set<MoMBoardEntity>().Add(new MoMBoardEntity
            {
                Id = seed.Id,
                SeasonId = seasonId,
                MixId = mixId,
                ChartType = (byte)seed.ChartType,
                // The same wrapper shape the legacy Configuration column carried, so every
                // board — migrated or new — deserializes through one path.
                ScoringConfig = JsonSerializer.Serialize(
                    TournamentConfigurationJsonEntity.From(seed.Configuration))
            });

            foreach (var (chartId, level) in seed.SnapshotDeltas)
            {
                if (!taken.Add((mixId, chartId))) continue;

                database.Set<MoMChartLevelEntity>().Add(new MoMChartLevelEntity
                {
                    SeasonId = seasonId,
                    MixId = mixId,
                    ChartId = chartId,
                    Level = level
                });
            }
        }
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
