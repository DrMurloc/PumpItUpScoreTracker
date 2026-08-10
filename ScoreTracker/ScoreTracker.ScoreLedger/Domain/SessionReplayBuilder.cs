using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Rebuilds what a session's score batch WOULD have announced, from the journal it left
///     behind. The batch itself lives in memory and dies with the process; the journal does not,
///     so a run whose derived work never happened can be replayed from what it wrote
///     (docs/design/import-restart-recovery.md §5).
///     <para>
///         Pure on purpose. Every rule below is a rule
///         <see cref="Application.UpdatePhoenixRecordHandler" /> applies on the way in, and the
///         only way to know this agrees with it is to test it without a database in the way.
///     </para>
/// </summary>
internal static class SessionReplayBuilder
{
    /// <summary>What one chart contributed to the lost batch.</summary>
    internal sealed record ReplayedChange(Guid ChartId, bool IsNewPass, int? OldScore);

    /// <summary>
    ///     The charts a session moved, and how.
    /// </summary>
    /// <param name="mix">
    ///     ⚠ Required, and not decoration. <c>histories</c> comes from
    ///     <see cref="IScoreJournalRepository.GetChartHistories" />, which is deliberately
    ///     CROSS-MIX: a returning song carries one ChartId across Phoenix and Phoenix 2, so an
    ///     unfiltered "row before this one" can hand a Phoenix 1 play to a Phoenix 2 session as
    ///     its before-state — wrong OldScore, wrong IsNewPass, no error. The undo replay already
    ///     made this mistake once.
    /// </param>
    /// <param name="sessionEntries">Every journal row the session wrote, observations included.</param>
    /// <param name="histories">Full per-chart history for the session's charts, any mix, any order.</param>
    public static IReadOnlyList<ReplayedChange> Build(MixEnum mix,
        IReadOnlyCollection<ScoreJournalEntry> sessionEntries,
        IReadOnlyCollection<ScoreJournalEntry> histories)
    {
        // Observations never entered a batch — they are plays the site reported that never beat
        // a best, so nothing about the record changed and nothing was ever announced.
        var written = sessionEntries
            .Where(e => e.IsBest && e.Mix == mix)
            .OrderBy(e => e.OccurredAt)
            .ToArray();
        if (written.Length == 0) return Array.Empty<ReplayedChange>();

        var byChart = histories
            .Where(h => h.Mix == mix && h.IsBest)
            .GroupBy(h => h.ChartId)
            .ToDictionary(g => g.Key, g => g.OrderBy(h => h.OccurredAt).ToArray());

        var changes = new List<ReplayedChange>();
        foreach (var chartId in written.Select(e => e.ChartId).Distinct())
        {
            var chartRows = written.Where(e => e.ChartId == chartId).ToArray();
            var first = chartRows[0];
            var last = chartRows[^1];
            var before = StateBefore(byChart, chartId, first);

            // isNewScore: was broken or absent, and this run left it not broken. A chart the
            // session both passed and then improved is ONE new pass, never a pass plus an
            // upscore — the accumulator keeps NewCharts and UpscoreCharts disjoint and lets the
            // new pass win.
            var wasBrokenOrAbsent = before?.IsBroken ?? true;
            if (wasBrokenOrAbsent && !last.IsBroken)
            {
                changes.Add(new ReplayedChange(chartId, IsNewPass: true, OldScore: null));
                continue;
            }

            // isUpscore: both sides scored, and the session raised it. A plate-only improvement
            // is journaled and was deliberately never announced, so it is skipped here too.
            var oldScore = before?.Score;
            var newScore = last.Score;
            if (oldScore != null && newScore != null && (int)oldScore.Value < (int)newScore.Value)
                changes.Add(new ReplayedChange(chartId, IsNewPass: false, OldScore: (int)oldScore.Value));
        }

        return changes;
    }

    /// <summary>
    ///     The chart's recorded state immediately before this session touched it — the newest
    ///     history row older than the session's first row for that chart. Null means the session
    ///     is the first thing that ever recorded it.
    /// </summary>
    private static ScoreJournalEntry? StateBefore(Dictionary<Guid, ScoreJournalEntry[]> byChart,
        Guid chartId, ScoreJournalEntry firstInSession)
    {
        if (!byChart.TryGetValue(chartId, out var rows)) return null;
        return rows.LastOrDefault(r => r.OccurredAt < firstInSession.OccurredAt);
    }
}
