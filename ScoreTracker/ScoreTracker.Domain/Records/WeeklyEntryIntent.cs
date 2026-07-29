namespace ScoreTracker.Domain.Records;

/// <summary>
///     What a weekly-board submission is asking for (weekly-charts-overhaul.md §9.2). The
///     importer and the Record dialog send the same command against the same board and want
///     opposite things from an existing entry, so the command says which.
/// </summary>
public enum WeeklyEntryIntent
{
    /// <summary>
    ///     Merge field by field, keeping the better of each. Idempotent — replaying an import
    ///     can never move a board. The default, and what every raising submission wants.
    /// </summary>
    BestWins,

    /// <summary>
    ///     The submitted entry becomes the entry, lower score included — a player correcting
    ///     their own self-report. Refused against an <see cref="ChallengeEntrySource.Official" />
    ///     entry (§9.4).
    /// </summary>
    Replace
}
