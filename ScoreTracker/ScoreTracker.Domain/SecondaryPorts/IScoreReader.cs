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
    /// <summary>A player's best attempt per chart.</summary>
    Task<IEnumerable<RecordedPhoenixScore>> GetBestScores(Guid userId, CancellationToken cancellationToken);

    /// <summary>Bulk read for analytics: every player's best attempt in a level×type folder.</summary>
    Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> GetScores(ChartType chartType,
        DifficultyLevel level, CancellationToken cancellationToken);

    /// <summary>Best attempts for a set of players within a level range.</summary>
    Task<IEnumerable<RecordedPhoenixScore>> GetScores(IEnumerable<Guid> userIds, ChartType chartType,
        DifficultyLevel minimumLevel, DifficultyLevel maximumLevel, CancellationToken cancellationToken);

    /// <summary>Players holding a Perfect Game in a level×type folder.</summary>
    Task<IEnumerable<(Guid UserId, Guid ChartId)>> GetPgUsers(ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken);

    /// <summary>Best attempts for a set of players in a level×type folder.</summary>
    Task<IEnumerable<(Guid userId, RecordedPhoenixScore record)>> GetPlayerScores(IEnumerable<Guid> userIds,
        ChartType chartType, DifficultyLevel difficulty, CancellationToken cancellationToken = default);

    /// <summary>Named best attempts for a set of players across a set of charts.</summary>
    Task<IEnumerable<UserPhoenixScore>> GetPlayerScores(IEnumerable<Guid> userIds,
        IEnumerable<Guid> chartIds, CancellationToken cancellationToken = default);

    /// <summary>Named best attempts for a set of players on one chart.</summary>
    Task<IEnumerable<UserPhoenixScore>> GetPhoenixScores(IEnumerable<Guid> userIds, Guid chartId,
        CancellationToken cancellationToken = default);

    /// <summary>How many charts a player has cleared in a level×type folder.</summary>
    Task<int> GetClearCount(Guid userId, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     A player's journaled submission history on one chart, oldest first. Entries are
    ///     submissions as received (including ones that didn't beat the stored best), so
    ///     scores are not monotonic. History begins at the journal backfill (2026-06).
    /// </summary>
    Task<IEnumerable<ScoreJournalEntry>> GetScoreHistory(Guid userId, Guid chartId,
        CancellationToken cancellationToken = default);

    /// <summary>Users with any recorded best-attempt activity on or after the cutoff.</summary>
    Task<IReadOnlySet<Guid>> GetActiveUserIds(DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>A player's best XX (legacy mix) attempt per chart. XX records are Ledger-owned too.</summary>
    Task<IEnumerable<BestXXChartAttempt>> GetBestXXAttempts(Guid userId,
        CancellationToken cancellationToken = default);
}
