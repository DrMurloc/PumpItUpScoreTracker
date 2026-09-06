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

/// <summary>The (mix, chart type) pair that identifies a board within its season (D3).</summary>
internal sealed record MoMBoardKey(MixEnum Mix, ChartType ChartType);

/// <summary>
///     The season cycle's storage: quarterly lookups, atomic season + boards + snapshot
///     creation, and the D13 prune. Boards, chart levels and sessions cascade with their
///     season, so a prune is one delete.
/// </summary>
internal interface IMoMRepository
{
    Task<MoMSeason?> GetSeason(int year, int quarter, CancellationToken cancellationToken);

    Task CreateSeason(MoMSeason season, IReadOnlyList<MoMBoardSeed> boards,
        CancellationToken cancellationToken);

    /// <summary>The (mix, chart type) pairs a season already has boards for.</summary>
    Task<IReadOnlyList<MoMBoardKey>> GetBoardKeys(Guid seasonId, CancellationToken cancellationToken);

    /// <summary>
    ///     Seats boards on an existing season, with their snapshot delta rows — the heal of
    ///     D43. A snapshot row the season already holds for that (mix, chart) is left alone.
    /// </summary>
    Task AddBoards(Guid seasonId, IReadOnlyList<MoMBoardSeed> boards, CancellationToken cancellationToken);

    /// <summary>Deletes every ended season with no sessions on any board (D13).</summary>
    Task PruneEndedEmptySeasons(DateTimeOffset now, CancellationToken cancellationToken);
}
