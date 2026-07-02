using System.Collections.Concurrent;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Accessors;

public sealed class PlayerScoreBatchAccumulator : IPlayerScoreBatchAccumulator
{
    private sealed class BatchState
    {
        public readonly object Gate = new();
        public DateTime FireAt;
        public readonly HashSet<Guid> NewCharts = new();
        public readonly Dictionary<Guid, PhoenixScore> UpscoreCharts = new();
    }

    private readonly ConcurrentDictionary<Guid, BatchState> _batches = new();

    public bool AddToBatch(Guid userId, DateTime fireAt, Guid chartId, bool isNewClear, PhoenixScore? upscoredFrom)
    {
        // Loop guards a race where TakeBatch removes our state between GetOrAdd and
        // acquiring the lock — in that case we'd be writing to an orphaned state, so
        // we drop and re-add a fresh one.
        while (true)
        {
            var fresh = new BatchState();
            var state = _batches.GetOrAdd(userId, fresh);
            var isNew = ReferenceEquals(state, fresh);
            lock (state.Gate)
            {
                if (!_batches.TryGetValue(userId, out var current) || !ReferenceEquals(current, state))
                    continue;
                state.FireAt = fireAt;
                if (isNewClear) state.NewCharts.Add(chartId);
                if (upscoredFrom.HasValue && !state.NewCharts.Contains(chartId))
                    state.UpscoreCharts[chartId] = upscoredFrom.Value;
                return isNew;
            }
        }
    }

    public DateTime? GetFireAt(Guid userId)
    {
        if (!_batches.TryGetValue(userId, out var state)) return null;
        lock (state.Gate) return state.FireAt;
    }

    public PendingScoreBatch? TakeBatch(Guid userId)
    {
        if (!_batches.TryGetValue(userId, out var state)) return null;
        lock (state.Gate)
        {
            if (!_batches.TryGetValue(userId, out var current) || !ReferenceEquals(current, state))
                return null;
            var newCharts = state.NewCharts.ToArray();
            var upscores = state.UpscoreCharts.ToDictionary(kv => kv.Key, kv => (int)kv.Value);
            _batches.TryRemove(userId, out _);
            return new PendingScoreBatch(newCharts, upscores);
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
                return new BatchAccumulatorSnapshotEntry(kv.Key, kv.Value.FireAt, newCharts, upscores);
            }
        }).ToArray();
    }
}
