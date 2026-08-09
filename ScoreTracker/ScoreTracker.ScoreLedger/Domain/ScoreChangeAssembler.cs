using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Turns a pending batch plus the records it moved into the announcement's changes.
///     <para>
///         Shared by the live drain and by the restart replay so the two cannot disagree about
///         what a batch means. Note where the "after" state comes from: the CURRENT best-attempt
///         records, never the journal rows — the journal says what a play was, the record says
///         what the chart stands at, and the announcement has always described the latter.
///     </para>
/// </summary>
internal static class ScoreChangeAssembler
{
    public static PlayerScoresUpdatedEvent.ScoreChange[] Build(PendingScoreBatch batch,
        IReadOnlyDictionary<Guid, RecordedPhoenixScore> bests)
    {
        return batch.NewChartIds.Concat(batch.UpscoredChartIds.Keys).ToHashSet()
            .Select(chartId =>
            {
                var best = bests.GetValueOrDefault(chartId);
                return new PlayerScoresUpdatedEvent.ScoreChange(
                    chartId,
                    IsNewPass: !batch.UpscoredChartIds.ContainsKey(chartId),
                    OldScore: batch.UpscoredChartIds.TryGetValue(chartId, out var old) ? old : null,
                    NewScore: best?.Score,
                    Plate: best?.Plate?.ToString(),
                    IsBroken: best?.IsBroken ?? false);
            })
            .ToArray();
    }
}
