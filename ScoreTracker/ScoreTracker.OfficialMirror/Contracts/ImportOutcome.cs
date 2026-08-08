namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     How an import run ended. A closed vocabulary on purpose: this reaches a player's screen,
///     and raw exception text never may (DiagnosticExposureTests). Whatever actually threw is in
///     the log, never in this value.
///     <para>
///         There is deliberately no member for "still running" or "never came back" — both are the
///         ABSENCE of an outcome, which is why the stored column is nullable. A row with no
///         FinishedAt and no Outcome is a run nothing ever closed, and the in-memory transport
///         drops in-flight messages on restart, so every deploy landing mid-import leaves one.
///     </para>
/// </summary>
public enum ImportOutcome
{
    /// <summary>
    ///     Ran to the end. Says nothing about how many scores it saved: finding nothing new is
    ///     the ordinary result of importing twice in a row, and is not a failure.
    /// </summary>
    Completed,

    /// <summary>piugame.com failed the run — a timeout, a reset connection, an error page.</summary>
    PiuGameError,

    /// <summary>PIU Scores failed the run. Whatever it was, it is ours and it is in the log.</summary>
    PiuScoresError
}
