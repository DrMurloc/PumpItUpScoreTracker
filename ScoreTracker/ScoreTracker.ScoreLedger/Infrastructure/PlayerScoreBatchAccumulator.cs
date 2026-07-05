using System.Collections.Concurrent;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
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
        public readonly HashSet<Guid> NewCharts = new();
        public readonly Dictionary<Guid, PhoenixScore> UpscoreCharts = new();
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
    // sessions and the next submission starts a fresh one.
    private static readonly TimeSpan SessionGap = TimeSpan.FromHours(8);

    // Keyed per (user, mix): parallel-mix submissions accumulate independently.
    private readonly ConcurrentDictionary<(Guid UserId, MixEnum Mix), BatchState> _batches = new();

    private readonly ConcurrentDictionary<(Guid UserId, MixEnum Mix, string Source), SessionState> _sessions = new();

    public Guid GetOrExtendSession(MixEnum mix, Guid userId, string source, DateTimeOffset now,
        Guid? explicitSessionId = null)
    {
        var state = _sessions.GetOrAdd((userId, mix, source), _ => new SessionState());
        lock (state)
        {
            if (explicitSessionId != null)
                state.Id = explicitSessionId.Value;
            else if (state.Id == Guid.Empty || now - state.LastActivity > SessionGap)
                state.Id = Guid.NewGuid();

            state.LastActivity = now;
            return state.Id;
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
                // landing mid-import) attributes to the most recent session.
                state.SessionId = sessionId;
                if (isNewClear) state.NewCharts.Add(chartId);
                if (upscoredFrom.HasValue && !state.NewCharts.Contains(chartId))
                    state.UpscoreCharts[chartId] = upscoredFrom.Value;
                return isNew;
            }
        }
    }

    public DateTime? GetFireAt(MixEnum mix, Guid userId)
    {
        if (!_batches.TryGetValue((userId, mix), out var state)) return null;
        lock (state.Gate) return state.FireAt;
    }

    public PendingScoreBatch? TakeBatch(MixEnum mix, Guid userId)
    {
        var key = (userId, mix);
        if (!_batches.TryGetValue(key, out var state)) return null;
        lock (state.Gate)
        {
            if (!_batches.TryGetValue(key, out var current) || !ReferenceEquals(current, state))
                return null;
            var newCharts = state.NewCharts.ToArray();
            var upscores = state.UpscoreCharts.ToDictionary(kv => kv.Key, kv => (int)kv.Value);
            _batches.TryRemove(key, out _);
            return new PendingScoreBatch(mix, newCharts, upscores, state.SessionId);
        }
    }

    public IReadOnlyCollection<BatchAccumulatorSnapshotEntry> Dump()
    {
        return _batches.ToArray().Select(kv =>
        {
            lock (kv.Value.Gate)
            {
                var newCharts = kv.Value.NewCharts.ToArray();
                var upscores = kv.Value.UpscoreCharts.ToDictionary(e => e.Key, e => (int)e.Value);
                return new BatchAccumulatorSnapshotEntry(kv.Key.UserId, kv.Key.Mix, kv.Value.FireAt, newCharts,
                    upscores);
            }
        }).ToArray();
    }
}
