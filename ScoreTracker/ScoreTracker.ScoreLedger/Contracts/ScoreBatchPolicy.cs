namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     When a run of scores stops being a run of scores and becomes one announcement.
///     <para>
///         Published because it is not only the Ledger's business: everything downstream of a
///         score batch — highlight capture, the session snapshot card, a page waiting for either
///         — is idle for this long first, and anything that wants to say "still working" has to
///         outlast it. A consumer that hard-codes its own two minutes instead is racing this one,
///         which is exactly what left the Sessions page clearing its patience card at the very
///         moment the batch fired.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public static class ScoreBatchPolicy
{
    /// <summary>
    ///     How long a batch waits before announcing itself — ⚠ measured from the LATEST score in
    ///     it, not the first. Every score pushes the deadline out again, so an import that writes
    ///     for six minutes announces nothing until two minutes after its last write.
    /// </summary>
    public static readonly TimeSpan HoldWindow = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     Slack between the deadline and the drain that acts on it, so the drain never arrives
    ///     to find the deadline a moment away and rescheduses itself.
    /// </summary>
    public static readonly TimeSpan DrainBuffer = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     How long a reader should keep expecting work after the last score lands: the hold, the
    ///     drain, and enough room for capture itself to run.
    ///     <para>
    ///         ⚠ Not a guarantee. A restart erases the accumulator, so a batch caught inside the
    ///         hold window is gone and its work does not land until the next process start recovers
    ///         it; a drain lost without a restart waits for the five-minute sweep instead
    ///         (docs/design/import-restart-recovery.md §4 and §4.3). Either way a reader that stops
    ///         waiting is simply early, which is why nothing may treat the end of this as proof
    ///         that there was nothing to wait for.
    ///     </para>
    /// </summary>
    public static readonly TimeSpan WorkExpectedWithin = HoldWindow + DrainBuffer + TimeSpan.FromMinutes(2);

    /// <summary>
    ///     How long after a run reports its ending the mid-life sweep waits before treating its
    ///     session as stranded.
    ///     <para>
    ///         ⚠ Deliberately NOT <see cref="WorkExpectedWithin" />, though it looks like the same
    ///         question. That constant is a *reader's* patience — how long a page keeps a spinner
    ///         up — and its two minutes of headroom past the drain is an assumption about how long
    ///         capture takes. The sweep needs the opposite bias: capture is what eventually stamps
    ///         the session processed, so anything short enough to be a good spinner is short enough
    ///         to declare a chain stranded while it is still running, and announce over the top of
    ///         it. Sharing one constant means every change to the spinner silently retunes the
    ///         double-announcement window.
    ///     </para>
    ///     <para>
    ///         Generous on purpose. The failure it recovers from ran eleven hours undetected, so
    ///         the difference between four minutes and half an hour costs nothing that matters,
    ///         and buys room for a first full-account import whose capture legitimately runs long.
    ///     </para>
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    /// <summary>
    ///     The gap that ends a sitting — the plays the plays endpoint records. A play joins the open
    ///     sitting when it was played within this of the sitting's plays, and a sitting closes and
    ///     announces itself once this long passes with no play arriving (docs/design/rise.md §12).
    /// </summary>
    public static readonly TimeSpan SittingQuietWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     How long past its last arrival an unannounced sitting waits before the sweep closes it: the
    ///     quiet window plus the sweep's own five-minute tick, so the scheduled close normally gets
    ///     there first and the sweep only catches one that was lost.
    /// </summary>
    public static readonly TimeSpan SittingOverdueAfter = SittingQuietWindow + TimeSpan.FromMinutes(5);

    /// <summary>
    ///     A sitting whose last play is older than this when it closes — a backlog a tool sent late —
    ///     records and captures as usual but posts no Discord card, so an old backlog cannot flood a
    ///     channel (docs/design/rise.md D23).
    /// </summary>
    public static readonly TimeSpan SittingCardCutoff = TimeSpan.FromDays(1);
}
