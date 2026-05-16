using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
/// Per-user score-update debouncer. UpdatePhoenixRecordHandler accumulates
/// new clears and upscores into a batch and schedules a single
/// PlayerScoreUpdatedEvent once the user has stopped recording for a while.
/// Implementation must be a singleton so state survives across handler instances
/// and must be safe for concurrent use across all members.
/// </summary>
public interface IPlayerScoreBatchAccumulator
{
    /// <summary>
    /// Atomically adds a chart update to the user's batch (creating the batch if
    /// needed) and pushes the fire-at time forward. If <paramref name="isNewClear"/>
    /// is true and <paramref name="upscoredFrom"/> is non-null for the same chart,
    /// new-clear takes precedence and the upscore is dropped.
    ///
    /// Returns true if this call created a new batch (caller should schedule a
    /// TryFireScoreMessage); false if a batch was already active (its fire-at has
    /// just been pushed forward).
    /// </summary>
    bool AddToBatch(Guid userId, DateTime fireAt, Guid chartId, bool isNewClear, PhoenixScore? upscoredFrom);

    /// <summary>Returns the scheduled fire-at, or null if no batch is active for the user.</summary>
    DateTime? GetFireAt(Guid userId);

    /// <summary>
    /// Atomically removes and returns the user's pending batch, or null if no batch
    /// is active (e.g. another in-flight drain already took it).
    /// </summary>
    PendingScoreBatch? TakeBatch(Guid userId);

    /// <summary>
    /// Diagnostic snapshot of every active batch. Best-effort — entries may be
    /// added or removed concurrently with the read.
    /// </summary>
    IReadOnlyCollection<BatchAccumulatorSnapshotEntry> Dump();
}
