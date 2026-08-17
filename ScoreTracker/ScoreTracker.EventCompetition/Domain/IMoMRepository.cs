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

    /// <summary>Deletes every ended season with no sessions on any board (D13).</summary>
    Task PruneEndedEmptySeasons(DateTimeOffset now, CancellationToken cancellationToken);
}
