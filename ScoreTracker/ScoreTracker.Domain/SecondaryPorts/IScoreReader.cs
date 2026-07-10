using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     The Score Ledger's published read contract (ADR-001 D3 "pull"): every consumer
///     outside the Ledger reads scores through this, never through
///     <see cref="IPhoenixRecordRepository" /> (which becomes Ledger-internal at P5).
///     Methods are added per consumer migration — additive only.
/// </summary>
public interface IScoreReader
{
    /// <summary>A player's best attempt per chart in a mix.</summary>
    Task<IEnumerable<RecordedPhoenixScore>> GetBestScores(MixEnum mix, Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Bulk read for analytics: every player's best attempt in a mix's level×type folder.</summary>
    Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> GetScores(MixEnum mix, ChartType chartType,
        DifficultyLevel level, CancellationToken cancellationToken);

    /// <summary>Best attempts for a set of players within a level range in a mix.</summary>
    Task<IEnumerable<RecordedPhoenixScore>> GetScores(MixEnum mix, IEnumerable<Guid> userIds, ChartType chartType,
        DifficultyLevel minimumLevel, DifficultyLevel maximumLevel, CancellationToken cancellationToken);

    /// <summary>Players holding a Perfect Game in a mix's level×type folder.</summary>
    Task<IEnumerable<(Guid UserId, Guid ChartId)>> GetPgUsers(MixEnum mix, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken);

    /// <summary>Best attempts for a set of players in a mix's level×type folder.</summary>
    Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetPlayerScores(MixEnum mix,
        IEnumerable<Guid> userIds,
        ChartType chartType, DifficultyLevel difficulty, CancellationToken cancellationToken = default);

    /// <summary>Named best attempts for a set of players across a set of charts in a mix.</summary>
    Task<IEnumerable<UserPhoenixScore>> GetPlayerScores(MixEnum mix, IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken = default);

    /// <summary>Named best attempts for a set of players on one chart in a mix.</summary>
    Task<IEnumerable<UserPhoenixScore>> GetPhoenixScores(MixEnum mix, IEnumerable<Guid> userIds, Guid chartId,
        CancellationToken cancellationToken = default);

    /// <summary>How many charts a player has cleared in a mix's level×type folder.</summary>
    Task<int> GetClearCount(MixEnum mix, Guid userId, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     A player's journaled submission history on one chart in a mix, oldest first. Entries are
    ///     submissions as received (including ones that didn't beat the stored best), so
    ///     scores are not monotonic. History begins at the journal backfill (2026-06).
    /// </summary>
    Task<IEnumerable<ScoreJournalEntry>> GetScoreHistory(MixEnum mix, Guid userId, Guid chartId,
        CancellationToken cancellationToken = default);

    /// <summary>Users with any recorded best-attempt activity in a mix on or after the cutoff.</summary>
    Task<IReadOnlySet<Guid>> GetActiveUserIds(MixEnum mix, DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Distinct calendar days with any journaled score event for the player in a mix.
    ///     The 2026-06 backfill dated rows at each record's last update, so this spans the
    ///     whole mix era as a lower bound on real play days.
    /// </summary>
    Task<int> GetPlayDayCount(MixEnum mix, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Per-chart population counts for a mix: players with a scored record, and how many passed.</summary>
    Task<IEnumerable<ChartScoreAggregate>> GetChartScoreAggregates(MixEnum mix,
        CancellationToken cancellationToken = default);

    /// <summary>A player's best XX (legacy mix) attempt per chart. XX records are Ledger-owned too.</summary>
    Task<IEnumerable<BestXXChartAttempt>> GetBestXXAttempts(Guid userId,
        CancellationToken cancellationToken = default);
}
