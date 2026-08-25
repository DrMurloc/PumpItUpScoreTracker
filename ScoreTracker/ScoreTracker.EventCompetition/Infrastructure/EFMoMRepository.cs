using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models;
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

    public async Task<IReadOnlyList<MoMSeason>> GetSeasons(CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<MoMSeasonEntity>()
            .Select(s => new MoMSeason(s.Id, s.Year, s.Quarter, s.Name, s.StartsAt, s.EndsAt,
                s.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MoMBoardRecord>> GetBoards(CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<MoMBoardEntity>().ToArrayAsync(cancellationToken))
            .Select(b => new MoMBoardRecord(b.Id, b.SeasonId, MixIds.ToEnum(b.MixId),
                (ChartType)b.ChartType))
            .ToArray();
    }

    public async Task<IReadOnlyList<MoMSessionRecord>> GetPublishedSessions(
        IReadOnlyCollection<Guid> boardIds, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<MoMSessionEntity>()
                .Where(s => boardIds.Contains(s.BoardId) && s.PublishedAt != null)
                .ToArrayAsync(cancellationToken))
            .Select(ToRecord)
            .ToArray();
    }

    public async Task<MoMSessionRecord?> GetSession(Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<MoMSessionEntity>()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        return entity == null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<MoMSessionChartRecord>> GetSessionCharts(Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<MoMSessionChartEntity>()
            .Where(c => c.SessionId == sessionId)
            .OrderBy(c => c.Ordinal)
            .Select(c => new MoMSessionChartRecord(c.Ordinal, c.ChartId, c.Score, c.Plate,
                c.IsBroken, c.SessionScore, c.BonusPoints, c.PlayedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<MoMSessionRecord?> GetDraft(Guid boardId, Guid userId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<MoMSessionEntity>()
            .Where(s => s.BoardId == boardId && s.UserId == userId && s.PublishedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity == null ? null : ToRecord(entity);
    }

    public async Task<TournamentConfiguration?> GetBoardConfiguration(Guid boardId,
        bool includeSnapshot, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var board = await database.Set<MoMBoardEntity>()
            .FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null) return null;

        var season = await database.Set<MoMSeasonEntity>()
            .SingleAsync(s => s.Id == board.SeasonId, cancellationToken);
        var snapshot = includeSnapshot
            ? await SnapshotFor(database, board, cancellationToken)
            : null;
        var json = JsonSerializer.Deserialize<TournamentConfigurationJsonEntity>(board.ScoringConfig)
                   ?? throw new InvalidOperationException(
                       $"MoM board {board.Id} has no scoring configuration");
        var frozen = json.To(snapshot == null ? null : new Dictionary<Guid, double>(snapshot));
        // The board pins the mix, so grading follows the board rather than defaulting to
        // Phoenix (§2.3) — a no-op for every Phoenix board, load-bearing once P2 boards exist.
        frozen.Scoring.Mix = MixIds.ToEnum(board.MixId);

        return new TournamentConfiguration(board.Id, season.Name, frozen.Scoring,
            isHighlighted: false, isMom: true)
        {
            StartDate = season.StartsAt,
            EndDate = season.EndsAt,
            MaxTime = frozen.MaxTime,
            AllowRepeats = frozen.AllowRepeats
        };
    }

    public async Task<IReadOnlyDictionary<Guid, double>> GetSeasonSnapshot(Guid boardId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var board = await database.Set<MoMBoardEntity>()
            .FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null) return new Dictionary<Guid, double>();
        return await SnapshotFor(database, board, cancellationToken);
    }

    private static async Task<Dictionary<Guid, double>> SnapshotFor(ChartAttemptDbContext database,
        MoMBoardEntity board, CancellationToken cancellationToken)
    {
        // Delta rows only (§9.3): a chart with no row prices at folder level + 0.5, which is
        // byte-identical to the scoring fallback for a missing key — sparse is exact.
        return await database.Set<MoMChartLevelEntity>()
            .Where(l => l.SeasonId == board.SeasonId && l.MixId == board.MixId)
            .ToDictionaryAsync(l => l.ChartId, l => l.Level, cancellationToken);
    }

    private static MoMSessionRecord ToRecord(MoMSessionEntity entity)
    {
        return new MoMSessionRecord(entity.Id, entity.BoardId, entity.UserId, entity.PublishedAt,
            entity.TotalScore, entity.ChartsPlayed, entity.RestTime, entity.AverageDifficulty,
            entity.AverageGrade, entity.LowestLevel, entity.HighestLevel, entity.VideoUrl);
    }
}
