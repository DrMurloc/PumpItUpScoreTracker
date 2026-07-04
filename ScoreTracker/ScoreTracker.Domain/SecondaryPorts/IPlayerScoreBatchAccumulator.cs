using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
/// Per-(user, mix) score-update debouncer. UpdatePhoenixRecordHandler accumulates
/// new clears and upscores into a batch and schedules a single
/// PlayerScoresUpdatedEvent once the user has stopped recording for a while.
/// Batches are keyed by user AND mix so parallel-mix submissions (Phoenix vs
/// Phoenix 2) never blend into one announcement.
/// Implementation must be a singleton so state survives across handler instances
/// and must be safe for concurrent use across all members.
/// </summary>
public interface IPlayerScoreBatchAccumulator
{
    /// <summary>
    /// Atomically adds a chart update to the (user, mix) batch (creating the batch if
    /// needed) and pushes the fire-at time forward. If <paramref name="isNewClear"/>
    /// is true and <paramref name="upscoredFrom"/> is non-null for the same chart,
    /// new-clear takes precedence and the upscore is dropped.
    ///
    /// Returns true if this call created a new batch (caller should schedule a
    /// TryFireScoreCommand); false if a batch was already active (its fire-at has
    /// just been pushed forward).
    /// </summary>
    bool AddToBatch(MixEnum mix, Guid userId, DateTime fireAt, Guid chartId, bool isNewClear,
        PhoenixScore? upscoredFrom);

    /// <summary>Returns the scheduled fire-at, or null if no batch is active for the (user, mix).</summary>
    DateTime? GetFireAt(MixEnum mix, Guid userId);

    /// <summary>
    /// Atomically removes and returns the (user, mix) pending batch, or null if no batch
    /// is active (e.g. another in-flight drain already took it).
    /// </summary>
    PendingScoreBatch? TakeBatch(MixEnum mix, Guid userId);

    /// <summary>
    /// Diagnostic snapshot of every active batch. Best-effort — entries may be
    /// added or removed concurrently with the read.
    /// </summary>
    IReadOnlyCollection<BatchAccumulatorSnapshotEntry> Dump();
}
