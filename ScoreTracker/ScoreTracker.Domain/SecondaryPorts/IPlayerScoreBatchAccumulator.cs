using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
/// Per-user score-update debouncer. UpdatePhoenixRecordHandler accumulates
/// new clears and upscores into a batch and schedules a single
/// PlayerScoreUpdatedEvent once the user has stopped recording for a while.
/// Implementation must be a singleton so state survives across handler instances.
/// </summary>
public interface IPlayerScoreBatchAccumulator
{
    /// <summary>
    /// Sets the fire-at time for this user. Returns true if no batch existed yet
    /// (caller should schedule the fire message); false if a batch was already active
    /// (its fire time has just been pushed forward).
    /// </summary>
    bool RegisterFireAt(Guid userId, DateTime fireAt);

    void RecordNewChart(Guid userId, Guid chartId);

    /// <summary>
    /// Records an upscore against the user's batch UNLESS the chart is already
    /// being tracked as a new clear (a new clear with a higher score takes precedence).
    /// </summary>
    void RecordUpscoreIfNotNew(Guid userId, Guid chartId, PhoenixScore previousScore);

    /// <summary>Returns the scheduled fire-at time. Throws if no batch is active for the user.</summary>
    DateTime GetFireAt(Guid userId);

    /// <summary>Atomically removes and returns the user's pending batch.</summary>
    PendingScoreBatch TakeBatch(Guid userId);
}
