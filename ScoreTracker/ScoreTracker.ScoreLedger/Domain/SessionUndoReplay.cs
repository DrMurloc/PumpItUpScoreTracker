using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Rebuilds a chart's best attempt from the plays that survive an undo.
///     Undo is surgical, not a rewind: it removes one session's rows and nothing else, so what a
///     chart should hold afterwards is simply what the remaining plays produce. Replaying is what
///     makes that true — deleting the rows alone would leave the session's scores standing as the
///     record, which is the opposite of undoing it.
///     Pure by design. Every case that matters is a table of plays and an expected winner, so it
///     tests with no doubles at all.
/// </summary>
internal static class SessionUndoReplay
{
    /// <summary>
    ///     The play that ends up the record, or null when nothing survives and the record should
    ///     go entirely.
    ///     Walks in time order and applies the same authority rule the write path does: an
    ///     acquisition source may only raise a record and goes through
    ///     <see cref="BestAttemptPolicy" />, while a manual entry or a CSV upload is authoritative
    ///     and overwrites. Taking a plain maximum instead would resurrect a score the player had
    ///     deliberately corrected downward.
    /// </summary>
    public static ScoreJournalEntry? BestOf(IEnumerable<ScoreJournalEntry> surviving)
    {
        ScoreJournalEntry? best = null;
        foreach (var play in surviving.OrderBy(p => p.OccurredAt))
        {
            if (best == null || IsAuthoritative(play))
            {
                best = play;
                continue;
            }

            if (BestAttemptPolicy.Beats(best.Score, best.Plate, best.IsBroken,
                    play.Score, play.Plate, play.IsBroken))
                best = play;
        }

        return best;
    }

    /// <summary>
    ///     Sources a human meant: the four record forms, the public API, and CSV upload — the
    ///     only routes by which a personal best may decrease (score-truth-model.md D9).
    /// </summary>
    private static bool IsAuthoritative(ScoreJournalEntry play)
    {
        return play.Source is ScoreJournalEntry.ManualSource or ScoreJournalEntry.CsvSource;
    }
}
