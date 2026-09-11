namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>What <see cref="IScoreJournalRepository.Append" /> did with a record's play.</summary>
internal enum JournalAppend
{
    /// <summary>A row now carries the play as the best: a new one, or the same play already journaled.</summary>
    Written,

    /// <summary>
    ///     A different play already holds the entry's time, so nothing was written. A Phoenix 2
    ///     best-list card keeps its chart's first-play date as the score improves, which is how a best
    ///     arrives wearing an earlier play's time.
    /// </summary>
    TimeHeldByAnotherPlay
}
