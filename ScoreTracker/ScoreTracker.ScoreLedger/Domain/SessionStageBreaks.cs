using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Re-solves a session's judged stage breaks on the given charts together once a write has
///     landed, so a replay recorded now can settle a run the same session recorded earlier. Only the
///     rows whose cause changes are written back.
/// </summary>
internal static class SessionStageBreaks
{
    public static async Task Resolve(IScoreJournalRepository journal, Guid userId, MixEnum mix, Guid sessionId,
        IReadOnlyDictionary<Guid, ChartFacts> charts, CancellationToken cancellationToken)
    {
        var rows = await journal.GetSessionEntries(userId, sessionId, cancellationToken);
        var changed = new List<(Guid ChartId, DateTimeOffset OccurredAt, StageBreakCause Cause)>();
        foreach (var chart in rows
                     .Where(e => e.Mix == mix && e.IsStageBroken && e.Judgements != null &&
                                 charts.ContainsKey(e.ChartId))
                     .GroupBy(e => e.ChartId))
        {
            var streak = chart.OrderBy(e => e.OccurredAt).ToArray();
            if (streak.Length < 2) continue;

            var causes = NoteCountWatch.CausesFor(streak.Select(e => e.Judgements!).ToArray(), charts[chart.Key],
                mix);
            for (var i = 0; i < streak.Length; i++)
                if (causes[i] != streak[i].Cause)
                    changed.Add((chart.Key, streak[i].OccurredAt, causes[i]));
        }

        if (changed.Count > 0) await journal.SetStageBreakCauses(userId, mix, changed, cancellationToken);
    }
}
