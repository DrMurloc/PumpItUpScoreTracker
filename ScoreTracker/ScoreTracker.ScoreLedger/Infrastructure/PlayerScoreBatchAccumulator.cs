using System.Collections.Concurrent;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Infrastructure;

// The "Session Batcher": moved here from Web.Accessors (it never had an ASP.NET
// dependency) when it grew session envelopes alongside its 2-minute event batches.
internal sealed class PlayerScoreBatchAccumulator : IPlayerScoreBatchAccumulator
{
    private sealed class BatchState
    {
        public readonly object Gate = new();
        public DateTime FireAt;
        // The same fold an import's announcement uses, so a batch and an import count one chart the
        // same way: a new pass wins, an upscore keeps the score it had before the batch.
        public readonly ScoreChangeFold Changes = new();
        public Guid? SessionId;
    }

    private sealed class SessionState
    {
        public Guid Id;
        public DateTimeOffset LastActivity;
    }

    // A session envelope groups journal rows across event batches: same (user, mix,
    // source) within the gap = one session. Envelopes are identity only — they never
    // delay the 2-minute event batches. In-memory by design: a restart closes open
    // sessions and the next submission starts a fresh one. Only writes without an id of
    // their own ride it; the read side folds recorded imports on the same silence
    // (SessionFold), which is why the gap is that constant rather than a second eight.
    private static readonly TimeSpan SessionGap = SessionFold.QuietGap;

    // Keyed per (user, mix): parallel-mix submissions accumulate independently.
    private readonly ConcurrentDictionary<(Guid UserId, MixEnum Mix), BatchState> _batches = new();

    private readonly ConcurrentDictionary<(Guid UserId, MixEnum Mix, string Source), SessionState> _sessions = new();

    public (Guid Id, bool IsNew) GetOrExtendSession(MixEnum mix, Guid userId, string source, DateTimeOffset now,
        Guid? explicitSessionId = null)
    {
        var state = _sessions.GetOrAdd((userId, mix, source), _ => new SessionState());
        lock (state)
        {
            var isNew = false;
            if (explicitSessionId != null)
            {
                // An explicit id belongs to a caller that opened the session itself (an import
                // run), so it is never reported as new — recording it twice is that caller's
                // problem to not have.
                state.Id = explicitSessionId.Value;
            }
            else if (state.Id == Guid.Empty || now - state.LastActivity > SessionGap)
            {
                state.Id = Guid.NewGuid();
                isNew = true;
            }

            state.LastActivity = now;
            return (state.Id, isNew);
        }
    }

    public void ForgetSession(Guid userId, MixEnum mix, Guid sessionId, IReadOnlyCollection<Guid> chartIds)
    {
        foreach (var (key, state) in _sessions.ToArray())
        {
            if (key.UserId != userId || key.Mix != mix) continue;
            lock (state)
            {
                if (state.Id == sessionId) _sessions.TryRemove(key, out _);
            }
        }

        var batchKey = (userId, mix);
        if (!_batches.TryGetValue(batchKey, out var batch)) return;
        lock (batch.Gate)
        {
            // Same orphaned-state guard as AddToBatch. A batch the next submission already relabelled
            // is that submission's to announce, and is left alone.
            if (!_batches.TryGetValue(batchKey, out var current) || !ReferenceEquals(current, batch) ||
                batch.SessionId != sessionId)
                return;
            foreach (var chartId in chartIds) batch.Changes.Remove(chartId);
            if (batch.Changes.IsEmpty)
                _batches.TryRemove(batchKey, out _);
            else
                // What is left belongs to no surviving session: announced, but under none.
                batch.SessionId = null;
        }
    }

    public bool AddToBatch(MixEnum mix, Guid userId, DateTime fireAt, Guid chartId, bool isNewClear,
        PhoenixScore? upscoredFrom, Guid sessionId)
    {
        var key = (userId, mix);
        // Loop guards a race where TakeBatch removes our state between GetOrAdd and
        // acquiring the lock — in that case we'd be writing to an orphaned state, so
        // we drop and re-add a fresh one.
        while (true)
        {
            var fresh = new BatchState();
            var state = _batches.GetOrAdd(key, fresh);
            var isNew = ReferenceEquals(state, fresh);
            lock (state.Gate)
            {
                if (!_batches.TryGetValue(key, out var current) || !ReferenceEquals(current, state))
                    continue;
                state.FireAt = fireAt;
                // Last submission wins: a batch that mixes sources (rare — a manual entry
                // landing mid-upload) attributes to the most recent session.
                state.SessionId = sessionId;
                state.Changes.Add(chartId,
                    isNewClear ? ScoreSaveChange.NewPass
                    : upscoredFrom.HasValue ? ScoreSaveChange.Upscore
                    : ScoreSaveChange.None,
                    upscoredFrom.HasValue ? (int)upscoredFrom.Value : null);
                return isNew;
            }
        }
    }

    public DateTime? GetFireAt(MixEnum mix, Guid userId)
    {
        if (!_batches.TryGetValue((userId, mix), out var state)) return null;
        lock (state.Gate) return state.FireAt;
    }

    public bool MakeDue(MixEnum mix, Guid userId, DateTime dueAt)
    {
        var key = (userId, mix);
        if (!_batches.TryGetValue(key, out var state)) return false;
        lock (state.Gate)
        {
            // Same orphaned-state guard as AddToBatch: a drain may have taken this batch between the
            // lookup and the gate, and bringing a dead batch's deadline forward would drain nothing.
            if (!_batches.TryGetValue(key, out var current) || !ReferenceEquals(current, state))
                return false;
            if (state.FireAt > dueAt) state.FireAt = dueAt;
            return true;
        }
    }

    public PendingScoreBatch? TakeBatch(MixEnum mix, Guid userId)
    {
        var key = (userId, mix);
        if (!_batches.TryGetValue(key, out var state)) return null;
        lock (state.Gate)
        {
            if (!_batches.TryGetValue(key, out var current) || !ReferenceEquals(current, state))
                return null;
            _batches.TryRemove(key, out _);
            return state.Changes.ToBatch(mix, state.SessionId);
        }
    }

    public IReadOnlyCollection<DueScoreBatch> TakeDueBatches(DateTime dueBefore)
    {
        var taken = new List<DueScoreBatch>();
        foreach (var (key, state) in _batches.ToArray())
            lock (state.Gate)
            {
                // Same orphaned-state guard as AddToBatch: a concurrent TakeBatch may have
                // removed and replaced this state between the snapshot and the gate.
                if (!_batches.TryGetValue(key, out var current) || !ReferenceEquals(current, state))
                    continue;
                // FireAt is stamped under this gate, and AddToBatch publishes the state before
                // taking it — so an unstamped batch is brand new, not overdue since year one.
                if (state.FireAt == default || state.FireAt > dueBefore) continue;
                _batches.TryRemove(key, out _);
                taken.Add(new DueScoreBatch(key.UserId, state.Changes.ToBatch(key.Mix, state.SessionId)));
            }

        return taken;
    }

    public IReadOnlyCollection<BatchAccumulatorSnapshotEntry> Dump()
    {
        return _batches.ToArray().Select(kv =>
        {
            lock (kv.Value.Gate)
            {
                return new BatchAccumulatorSnapshotEntry(kv.Key.UserId, kv.Key.Mix, kv.Value.FireAt,
                    kv.Value.Changes.NewPasses.ToArray(),
                    kv.Value.Changes.UpscoredFrom.ToDictionary(e => e.Key, e => e.Value));
            }
        }).ToArray();
    }
}
