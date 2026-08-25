using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.EventCompetition.Domain;

/// <summary>
///     A March of Murlocs season (docs/design/march-of-murlocs.md §6). Year/Quarter are NULL
///     only on the migrated off-grid legacy seasons; everything the cycle creates is quarterly.
/// </summary>
internal sealed record MoMSeason(Guid Id, int? Year, byte? Quarter, string Name,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset CreatedAt);

/// <summary>
///     One board to create with a season: its identity, the (mix, chart type) pair, the
///     configuration to freeze, and the season snapshot's delta rows for that mix (§9.3 —
///     a chart at folder level + 0.5 gets no row).
/// </summary>
internal sealed record MoMBoardSeed(Guid Id, MixEnum Mix, ChartType ChartType,
    TournamentConfiguration Configuration, IReadOnlyDictionary<Guid, double> SnapshotDeltas);

/// <summary>A stored board's identity — mix and chart type resolved from storage.</summary>
internal sealed record MoMBoardRecord(Guid Id, Guid SeasonId, MixEnum Mix, ChartType ChartType);

/// <summary>
///     A stored session's header row. Everything from TotalScore down is the derived cache
///     of its chart rows (§6); RestTimeTicks floors at zero — a session that overhangs the
///     window has no rest by construction.
/// </summary>
internal sealed record MoMSessionRecord(Guid Id, Guid BoardId, Guid UserId,
    DateTimeOffset? PublishedAt, int TotalScore, int ChartsPlayed, long RestTimeTicks,
    double AverageDifficulty, double AverageGrade, int LowestLevel, int HighestLevel,
    string? VideoUrl);

/// <summary>One stored chart row of a session, in entry order.</summary>
internal sealed record MoMSessionChartRecord(int Ordinal, Guid ChartId, int Score, string Plate,
    bool IsBroken, int SessionScore, int BonusPoints, DateTimeOffset? PlayedAt);

/// <summary>
///     The season cycle's storage plus the read surface the MoM pages consume: quarterly
///     lookups, atomic season + boards + snapshot creation, the D13 prune, and the
///     season/board/session reads. Boards, chart levels and sessions cascade with their
///     season, so a prune is one delete.
/// </summary>
internal interface IMoMRepository
{
    Task<MoMSeason?> GetSeason(int year, int quarter, CancellationToken cancellationToken);

    Task CreateSeason(MoMSeason season, IReadOnlyList<MoMBoardSeed> boards,
        CancellationToken cancellationToken);

    /// <summary>Deletes every ended season with no sessions on any board (D13).</summary>
    Task PruneEndedEmptySeasons(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Every season, unordered — callers order by StartsAt for prev/next walking.</summary>
    Task<IReadOnlyList<MoMSeason>> GetSeasons(CancellationToken cancellationToken);

    /// <summary>Every board of every season — a handful of rows per year.</summary>
    Task<IReadOnlyList<MoMBoardRecord>> GetBoards(CancellationToken cancellationToken);

    /// <summary>
    ///     Published session headers for the given boards, unranked — ranking (score
    ///     descending, earliest publication breaking ties, §1) is the caller's.
    /// </summary>
    Task<IReadOnlyList<MoMSessionRecord>> GetPublishedSessions(
        IReadOnlyCollection<Guid> boardIds, CancellationToken cancellationToken);

    /// <summary>The session by id, draft or published — visibility is the caller's rule.</summary>
    Task<MoMSessionRecord?> GetSession(Guid sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MoMSessionChartRecord>> GetSessionCharts(Guid sessionId,
        CancellationToken cancellationToken);

    /// <summary>The user's open draft on a board — at most one exists (§10).</summary>
    Task<MoMSessionRecord?> GetDraft(Guid boardId, Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     The board's frozen configuration, mix pinned and MaxTime/AllowRepeats read back
    ///     from the frozen JSON — a session always replays under exactly the rules it was
    ///     recorded under. Each call returns a fresh instance, safe to mutate. With
    ///     includeSnapshot false the season's chart-level snapshot is left off, which is the
    ///     seam the D20 re-rating split isolates its effects through.
    /// </summary>
    Task<TournamentConfiguration?> GetBoardConfiguration(Guid boardId, bool includeSnapshot,
        CancellationToken cancellationToken);

    /// <summary>
    ///     The board's season snapshot for its mix — delta rows only (§9.3): a chart with no
    ///     entry prices at folder level + 0.5.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, double>> GetSeasonSnapshot(Guid boardId,
        CancellationToken cancellationToken);
}
