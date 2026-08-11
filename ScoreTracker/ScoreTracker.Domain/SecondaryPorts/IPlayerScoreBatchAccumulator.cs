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
    /// Returns the open session id for (user, mix, source), extending its activity
    /// window — or mints a new one when none is open or the gap (8 h) has elapsed.
    /// An explicit id (an import/CSV run id) takes over the envelope. Session
    /// envelopes are identity only: they group journal rows and never delay the
    /// event batches below.
    ///
    /// <c>IsNew</c> is true only on the call that minted the id, so the caller can record
    /// the session once. This stays a pure in-memory decision — the accumulator is a
    /// concurrency primitive and never touches the database; persisting what it decided
    /// is the handler's job.
    /// </summary>
    (Guid Id, bool IsNew) GetOrExtendSession(MixEnum mix, Guid userId, string source, DateTimeOffset now,
        Guid? explicitSessionId = null);

    /// <summary>
    /// Atomically adds a chart update to the (user, mix) batch (creating the batch if
    /// needed) and pushes the fire-at time forward. If <paramref name="isNewClear"/>
    /// is true and <paramref name="upscoredFrom"/> is non-null for the same chart,
    /// new-clear takes precedence and the upscore is dropped. The batch carries the
    /// most recent <paramref name="sessionId"/> onto its published event.
    ///
    /// Returns true if this call created a new batch (caller should schedule a
    /// TryFireScoreCommand); false if a batch was already active (its fire-at has
    /// just been pushed forward).
    /// </summary>
    bool AddToBatch(MixEnum mix, Guid userId, DateTime fireAt, Guid chartId, bool isNewClear,
        PhoenixScore? upscoredFrom, Guid sessionId);

    /// <summary>Returns the scheduled fire-at, or null if no batch is active for the (user, mix).</summary>
    DateTime? GetFireAt(MixEnum mix, Guid userId);

    /// <summary>
    /// Atomically removes and returns the (user, mix) pending batch, or null if no batch
    /// is active (e.g. another in-flight drain already took it).
    /// </summary>
    PendingScoreBatch? TakeBatch(MixEnum mix, Guid userId);

    /// <summary>
    /// Parks site-detected title names (the badges no score can compute) so the open
    /// (user, mix) batch's snapshot card can announce them instead of a card of their own.
    /// Returns false when no batch is open to carry them — the caller announces them
    /// itself. Deposits accumulate, so a name is never dropped by a second deposit.
    /// Held in a slot of its own rather than on the batch: the drain removes the batch
    /// before the title step that reads these runs.
    /// </summary>
    bool TryAddDetectedTitles(MixEnum mix, Guid userId, IEnumerable<string> titles);

    /// <summary>
    /// Atomically removes and returns the parked site-detected titles for the (user, mix),
    /// or empty when none were parked. Removing on read is what keeps a title from being
    /// announced twice across a session's successive batches.
    /// </summary>
    string[] TakeDetectedTitles(MixEnum mix, Guid userId);

    /// <summary>
    /// Atomically removes and returns every batch whose fire-at has passed
    /// <paramref name="dueBefore"/>. The recovery sweep's entry point.
    ///
    /// One call rather than Dump-then-TakeBatch because those two decide and act against
    /// different instants: the sweep awaits a publish per batch, so by the tenth entry the
    /// snapshot's fire-at is seconds stale and a player who resumed playing has their extended,
    /// still-live batch seized and announced mid-set. Here the due test and the removal happen
    /// under the same gate, so a batch is only ever taken on a fire-at that is true right then.
    ///
    /// A batch whose fire-at is still unset is never due: AddToBatch publishes the state into
    /// the dictionary before it takes the gate to stamp it, so a read that wins that race sees
    /// default(DateTime), which is not "infinitely overdue".
    /// </summary>
    IReadOnlyCollection<DueScoreBatch> TakeDueBatches(DateTime dueBefore);

    /// <summary>
    /// Diagnostic snapshot of every active batch. Best-effort — entries may be
    /// added or removed concurrently with the read.
    /// </summary>
    IReadOnlyCollection<BatchAccumulatorSnapshotEntry> Dump();
}
