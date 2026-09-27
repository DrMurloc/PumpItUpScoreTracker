using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     What a run of saves changed, folded into one announcement's worth. The typed-entry batch and an
///     import's announcement both fold through here, and it is the rule the journal replay reconstructs
///     too (<see cref="SessionReplayBuilder" />): a chart that became a pass is a new pass however it got
///     there, and a chart that only went up keeps the score it had before the run — a chart raised twice
///     reads as one step from where the player started, not from the middle.
/// </summary>
internal sealed class ScoreChangeFold
{
    private readonly HashSet<Guid> _newPasses = new();
    private readonly Dictionary<Guid, int> _upscoredFrom = new();

    public IReadOnlyCollection<Guid> NewPasses => _newPasses;
    public IReadOnlyDictionary<Guid, int> UpscoredFrom => _upscoredFrom;

    public bool IsEmpty => _newPasses.Count == 0 && _upscoredFrom.Count == 0;

    public void Add(Guid chartId, ScoreSaveChange change, int? upscoredFrom)
    {
        switch (change)
        {
            case ScoreSaveChange.NewPass:
                _newPasses.Add(chartId);
                _upscoredFrom.Remove(chartId);
                break;
            case ScoreSaveChange.Upscore when upscoredFrom is { } from && !_newPasses.Contains(chartId):
                _upscoredFrom.TryAdd(chartId, from);
                break;
        }
    }

    /// <summary>Drops a chart the fold should no longer announce — one an undo has already rebuilt.</summary>
    public void Remove(Guid chartId)
    {
        _newPasses.Remove(chartId);
        _upscoredFrom.Remove(chartId);
    }

    public void Add(ScoreSaveResult save)
    {
        Add(save.ChartId, save.Change, save.UpscoredFrom);
    }

    public void Add(PendingScoreBatch batch)
    {
        foreach (var chartId in batch.NewChartIds) Add(chartId, ScoreSaveChange.NewPass, null);
        foreach (var (chartId, from) in batch.UpscoredChartIds) Add(chartId, ScoreSaveChange.Upscore, from);
    }

    public PendingScoreBatch ToBatch(MixEnum mix, Guid? sessionId)
    {
        return new PendingScoreBatch(mix, _newPasses.ToArray(), new Dictionary<Guid, int>(_upscoredFrom),
            sessionId);
    }
}
