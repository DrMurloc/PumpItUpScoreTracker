using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Infrastructure;

internal sealed class EFMoMRepository : IMoMRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly IMemoryCache _memoryCache;
    private readonly IDateTimeOffsetAccessor _dateTime;

    public EFMoMRepository(IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache memoryCache,
        IDateTimeOffsetAccessor dateTime)
    {
        _factory = factory;
        _memoryCache = memoryCache;
        _dateTime = dateTime;
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

    public async Task<Guid?> GetDraftId(Guid boardId, Guid userId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Newest first: if an older draft ever survived, the one the player was last in wins.
        return await database.Set<MoMSessionEntity>()
            .Where(s => s.BoardId == boardId && s.UserId == userId && s.PublishedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveSession(Guid sessionId, TournamentSession session, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<MoMSessionEntity>()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        var board = await database.Set<MoMBoardEntity>()
            .SingleAsync(b => b.Id == session.TournamentId, cancellationToken);
        var mix = MixIds.ToEnum(board.MixId);
        var snapshot = await database.Set<MoMChartLevelEntity>()
            .Where(l => l.SeasonId == board.SeasonId && l.MixId == board.MixId)
            .ToDictionaryAsync(l => l.ChartId, l => l.Level, cancellationToken);

        var now = _dateTime.Now;
        if (entity == null)
        {
            entity = new MoMSessionEntity
            {
                Id = sessionId,
                BoardId = session.TournamentId,
                UserId = session.UsersId,
                CreatedAt = now,
                // A new row is a draft. Only PublishSession stamps it, which is what puts it
                // on a board -- nothing here reaches the leaderboard.
                PublishedAt = null
            };
            await database.Set<MoMSessionEntity>().AddAsync(entity, cancellationToken);
        }

        // Everything below PublishedAt is a derived cache of the chart rows (§6), recomputed
        // wholesale on every save. Balanced level is the season snapshot's override where one
        // exists, folder level + 0.5 where none does (§9.3). An empty draft averages nothing.
        entity.UpdatedAt = now;
        entity.TotalScore = session.TotalScore;
        entity.ChartsPlayed = session.Entries.Count;
        entity.RestTime = session.CurrentRestTime.Ticks;
        entity.AverageDifficulty = session.Entries.Count == 0
            ? 0
            : session.Entries.Average(e =>
                snapshot.TryGetValue(e.Chart.Id, out var balanced) ? balanced : (int)e.Chart.Level + 0.5);
        entity.AverageGrade = session.Entries.Count == 0
            ? 0
            : session.Entries.Average(e => (int)e.Score.LetterGradeFor(mix));
        entity.LowestLevel = (byte)(session.Entries.Count == 0 ? 0 : session.Entries.Min(e => (int)e.Chart.Level));
        entity.HighestLevel = (byte)(session.Entries.Count == 0 ? 0 : session.Entries.Max(e => (int)e.Chart.Level));
        entity.VideoUrl = session.VideoUrl?.ToString();

        var existingCharts = await database.Set<MoMSessionChartEntity>()
            .Where(c => c.SessionId == sessionId).ToArrayAsync(cancellationToken);
        database.Set<MoMSessionChartEntity>().RemoveRange(existingCharts);
        await database.Set<MoMSessionChartEntity>().AddRangeAsync(session.Entries.Select(
            (e, ordinal) => new MoMSessionChartEntity
            {
                SessionId = sessionId,
                Ordinal = ordinal,
                ChartId = e.Chart.Id,
                Score = e.Score,
                Plate = e.Plate.ToString(),
                IsBroken = e.IsBroken,
                SessionScore = e.SessionScore,
                BonusPoints = e.BonusPoints,
                PlayedAt = e.PlayedAt
            }), cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
        Evict();
    }

    public async Task PublishSession(Guid sessionId, DateTimeOffset publishedAt,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<MoMSessionEntity>()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (entity is not { PublishedAt: null }) return;

        entity.PublishedAt = publishedAt;
        entity.UpdatedAt = publishedAt;
        await database.SaveChangesAsync(cancellationToken);
        Evict();
    }

    public async Task DeleteSession(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<MoMSessionEntity>()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (entity == null) return;

        // Chart rows cascade with the session.
        database.Set<MoMSessionEntity>().Remove(entity);
        await database.SaveChangesAsync(cancellationToken);
        Evict();
    }

    /// <summary>A board's rankings are cached, and a write to either side of a session ages them.</summary>
    private void Evict()
    {
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
