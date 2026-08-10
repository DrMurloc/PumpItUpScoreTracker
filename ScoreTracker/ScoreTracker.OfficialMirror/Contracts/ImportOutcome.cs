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

    /// <summary>
    ///     piugame turned the credentials away, or the account has no game profile yet. Its own
    ///     value rather than a PiuGameError because the two need opposite copy: this one is fixable
    ///     by the player and says so, where PiuGameError asks them to wait and retry. Folding them
    ///     together would tell somebody with a mistyped password that the site was down.
    /// </summary>
    CredentialRejected,

    /// <summary>PIU Scores failed the run. Whatever it was, it is ours and it is in the log.</summary>
    PiuScoresError,

    /// <summary>
    ///     The process went away while the run was still going. Not a failure anybody saw and not
    ///     a success — the scores it had already saved are real and kept, and whatever it had not
    ///     reached is simply absent.
    ///     <para>
    ///         Distinct from the absent outcome above rather than replacing it: a run with no
    ///         FinishedAt is one nothing has adjudicated <em>yet</em>, and this is the startup
    ///         recovery pass's verdict once it has
    ///         (docs/design/import-restart-recovery.md).
    ///     </para>
    /// </summary>
    Interrupted
}
