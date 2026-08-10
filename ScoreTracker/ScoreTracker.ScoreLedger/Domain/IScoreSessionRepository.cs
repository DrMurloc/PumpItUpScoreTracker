using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     The session envelope's durable half. The batch accumulator still decides session
///     identity in memory — it is a concurrency primitive and never touches the database — and
///     this records what it decided, so a session has a wall-clock time and a name long after
///     the process that minted it has gone.
/// </summary>
internal interface IScoreSessionRepository
{
    /// <summary>
    ///     Records a session the accumulator has just opened. Idempotent on the id: a session
    ///     already recorded is left alone, so a racing second writer cannot reset its start time.
    /// </summary>
    Task Open(Guid id, Guid userId, MixEnum mix, string source, string? accountTag, string? cardId,
        DateTimeOffset startedAt, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Extends a session and adds to its counts, at batch drain rather than per submission —
    ///     an import posts thousands of scores and must not post thousands of updates.
    /// </summary>
    Task Touch(Guid id, DateTimeOffset at, int newCount, int upscoreCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Overwrites the counts rather than adding to them, for a session whose batch is being
    ///     replayed. <see cref="Touch" /> adds — correct when a session drains as several batches
    ///     — but a replay recomputes the whole session's totals from the journal, and on the
    ///     crash-mid-chain path Touch has already run once.
    /// </summary>
    Task SetCounts(Guid id, DateTimeOffset at, int newCount, int upscoreCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records that everything downstream of this session's batch has run. Idempotent: a
    ///     session already stamped keeps its first timestamp.
    /// </summary>
    Task MarkProcessed(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sessions whose derived work never ran. Naturally tiny — every session predating the
    ///     column is backfilled as processed — so this is the cheap end to start a recovery pass
    ///     from (docs/design/import-restart-recovery.md §3.1).
    /// </summary>
    Task<IReadOnlyList<ScoreSessionRecord>> ListUnprocessed(CancellationToken cancellationToken = default);

    Task<ScoreSessionRecord?> Get(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Newest first — the order the Undo page lists them in.</summary>
    Task<IReadOnlyList<ScoreSessionRecord>> ListFor(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Each player's single most recent session. The backfill's whole scope — history is
    ///     deliberately out of reach, because a rebuild computes against today's state.
    /// </summary>
    Task<IReadOnlyList<ScoreSessionRecord>> ListLatestPerUser(CancellationToken cancellationToken = default);

    Task Delete(Guid id, CancellationToken cancellationToken = default);
}
