using System.Collections.Concurrent;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Web.Accessors;

public sealed class PlayerScoreBatchAccumulator : IPlayerScoreBatchAccumulator
{
    private readonly ConcurrentDictionary<Guid, DateTime> _fireAt = new();
    private readonly ConcurrentDictionary<Guid, ISet<Guid>> _newCharts = new();
    private readonly ConcurrentDictionary<Guid, IDictionary<Guid, PhoenixScore>> _upscoreCharts = new();

    public bool RegisterFireAt(Guid userId, DateTime fireAt)
    {
        var isNew = !_fireAt.ContainsKey(userId);
        if (isNew)
        {
            _newCharts[userId] = new HashSet<Guid>();
            _upscoreCharts[userId] = new ConcurrentDictionary<Guid, PhoenixScore>();
        }
        _fireAt[userId] = fireAt;
        return isNew;
    }

    public void RecordNewChart(Guid userId, Guid chartId)
    {
        _newCharts[userId].Add(chartId);
    }

    public void RecordUpscoreIfNotNew(Guid userId, Guid chartId, PhoenixScore previousScore)
    {
        if (!_newCharts[userId].Contains(chartId))
            _upscoreCharts[userId][chartId] = previousScore;
    }

    public DateTime GetFireAt(Guid userId) => _fireAt[userId];

    public PendingScoreBatch TakeBatch(Guid userId)
    {
        var newChartIds = _newCharts.TryGetValue(userId, out var newChart)
            ? newChart.ToArray()
            : Array.Empty<Guid>();
        var upscoredChartIds = _upscoreCharts.TryGetValue(userId, out var chart)
            ? chart.ToDictionary(kv => kv.Key, kv => (int)kv.Value)
            : new Dictionary<Guid, int>();
        _fireAt.TryRemove(userId, out _);
        _upscoreCharts.TryRemove(userId, out _);
        _newCharts.TryRemove(userId, out _);
        return new PendingScoreBatch(newChartIds, upscoredChartIds);
    }

    public IReadOnlyCollection<BatchAccumulatorSnapshotEntry> Dump()
    {
        return _fireAt.ToArray().Select(kv =>
        {
            var newChartIds = _newCharts.TryGetValue(kv.Key, out var newSet)
                ? newSet.ToArray()
                : Array.Empty<Guid>();
            var upscored = _upscoreCharts.TryGetValue(kv.Key, out var upscoreMap)
                ? (IReadOnlyDictionary<Guid, int>)upscoreMap.ToDictionary(e => e.Key, e => (int)e.Value)
                : new Dictionary<Guid, int>();
            return new BatchAccumulatorSnapshotEntry(kv.Key, kv.Value, newChartIds, upscored);
        }).ToArray();
    }
}
