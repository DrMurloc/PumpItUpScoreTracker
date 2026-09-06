using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.EventCompetition.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Infrastructure;

/// <summary>
///     The MoM tables, read as stored (docs/design/march-of-murlocs.md §12.2). Each method is
///     one query over the ids it is given; the shaping — ranking, the four numbers, the
///     re-pricing — is the Domain's, so this class never computes a figure a page prints.
/// </summary>
internal sealed class EFMoMReadRepository : IMoMReadRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFMoMReadRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<MoMSeason>> GetSeasons(CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<MoMSeasonEntity>()
                .OrderByDescending(s => s.StartsAt)
                .ToArrayAsync(cancellationToken))
            .Select(s => new MoMSeason(s.Id, s.Year, s.Quarter, s.Name, s.StartsAt, s.EndsAt, s.CreatedAt))
            .ToArray();
    }

    public async Task<IReadOnlyList<MoMBoardInfo>> GetBoards(IEnumerable<Guid> seasonIds,
        CancellationToken cancellationToken)
    {
        var ids = seasonIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<MoMBoardInfo>();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var boards = await database.Set<MoMBoardEntity>()
            .Where(b => ids.Contains(b.SeasonId))
            .ToArrayAsync(cancellationToken);
        return await Infos(database, boards, cancellationToken);
    }

    public async Task<MoMBoardInfo?> GetBoard(Guid boardId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var board = await database.Set<MoMBoardEntity>()
            .FirstOrDefaultAsync(b => b.Id == boardId, cancellationToken);
        if (board == null) return null;
        return (await Infos(database, new[] { board }, cancellationToken)).Single();
    }

    /// <summary>
    ///     A board's frozen configuration: the stored JSON, over the season's delta-only
    ///     snapshot for the board's mix (§9.3 — a chart with no row sits at folder + 0.5, which
    ///     is exactly the engine's fallback for a missing key). The board pins the mix, so
    ///     grading follows the board rather than defaulting to Phoenix (§2.3).
    /// </summary>
    private static async Task<IReadOnlyList<MoMBoardInfo>> Infos(ChartAttemptDbContext database,
        IReadOnlyList<MoMBoardEntity> boards, CancellationToken cancellationToken)
    {
        var seasonIds = boards.Select(b => b.SeasonId).Distinct().ToArray();
        var seasons = await database.Set<MoMSeasonEntity>()
            .Where(s => seasonIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var boardCounts = await database.Set<MoMBoardEntity>()
            .Where(b => seasonIds.Contains(b.SeasonId))
            .GroupBy(b => b.SeasonId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var levels = (await database.Set<MoMChartLevelEntity>()
                .Where(l => seasonIds.Contains(l.SeasonId))
                .ToArrayAsync(cancellationToken))
            .GroupBy(l => (l.SeasonId, l.MixId))
            .ToDictionary(g => g.Key, g => (IDictionary<Guid, double>)g.ToDictionary(l => l.ChartId, l => l.Level));
        return boards.Select(board =>
        {
            var season = seasons[board.SeasonId];
            var snapshot = levels.TryGetValue((board.SeasonId, board.MixId), out var s) ? s : new Dictionary<Guid, double>();
            var json = JsonSerializer.Deserialize<TournamentConfigurationJsonEntity>(board.ScoringConfig)
                       ?? throw new InvalidOperationException($"MoM board {board.Id} has no scoring configuration");
            var frozen = json.To(snapshot);
            var mix = MixIds.ToEnum(board.MixId);
            frozen.Scoring.Mix = mix;
            var configuration = new TournamentConfiguration(board.Id,
                DisplayName(season, board, boardCounts.GetValueOrDefault(board.SeasonId, 1)), frozen.Scoring, false, true)
            {
                StartDate = season.StartsAt,
                EndDate = season.EndsAt,
                MaxTime = frozen.MaxTime,
                AllowRepeats = frozen.AllowRepeats
            };
            return new MoMBoardInfo(board.Id, board.SeasonId, mix, (ChartType)board.ChartType, configuration);
        }).ToArray();
    }

    // The same formula EFTournamentRepository.BoardDisplayName uses for the legacy listing, so a
    // board is called one thing everywhere it is still called by name.
    private static string DisplayName(MoMSeasonEntity season, MoMBoardEntity board, int seasonBoardCount)
    {
        var baseName = season.Name.StartsWith("March of Murlocs", StringComparison.OrdinalIgnoreCase)
            ? season.Name
            : $"March of Murlocs {season.Name}";
        return season.Quarter == null && seasonBoardCount == 1
            ? baseName
            : $"{baseName} - {(ChartType)board.ChartType}s";
    }

    public async Task<IReadOnlyList<MoMStoredSession>> GetPublishedSessions(IEnumerable<Guid> boardIds,
        CancellationToken cancellationToken)
    {
        var ids = boardIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<MoMStoredSession>();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<MoMSessionEntity>()
                .Where(s => ids.Contains(s.BoardId) && s.PublishedAt != null)
                .ToArrayAsync(cancellationToken))
            .Select(Stored)
            .ToArray();
    }

    public async Task<MoMStoredSession?> GetSession(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<MoMSessionEntity>()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        return entity == null ? null : Stored(entity);
    }

    public async Task<IReadOnlyList<MoMStoredSessionChart>> GetSessionCharts(IEnumerable<Guid> sessionIds,
        CancellationToken cancellationToken)
    {
        var ids = sessionIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<MoMStoredSessionChart>();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<MoMSessionChartEntity>()
                .Where(c => ids.Contains(c.SessionId))
                .OrderBy(c => c.SessionId).ThenBy(c => c.Ordinal)
                .ToArrayAsync(cancellationToken))
            .Select(c => new MoMStoredSessionChart(c.SessionId, c.Ordinal, c.ChartId, PhoenixScore.From(c.Score),
                Enum.Parse<PhoenixPlate>(c.Plate), c.IsBroken, c.SessionScore, c.BonusPoints, c.PlayedAt))
            .ToArray();
    }

    private static MoMStoredSession Stored(MoMSessionEntity s)
    {
        return new MoMStoredSession(s.Id, s.BoardId, s.UserId, s.PublishedAt, s.TotalScore, s.ChartsPlayed,
            TimeSpan.FromTicks(s.RestTime), s.AverageDifficulty, s.AverageGrade, s.LowestLevel, s.HighestLevel,
            Uri.TryCreate(s.VideoUrl, UriKind.Absolute, out var video) ? video : null, s.CreatedAt);
    }
}
