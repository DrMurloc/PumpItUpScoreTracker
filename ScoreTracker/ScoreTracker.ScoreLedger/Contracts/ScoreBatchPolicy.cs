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
    ///         hold window is gone and its work does not land at all until the next process start
    ///         recovers it (docs/design/import-restart-recovery.md). A reader that stops waiting is
    ///         then simply early, which is why nothing may treat the end of this as proof that
    ///         there was nothing to wait for.
    ///     </para>
    ///     <para>
    ///         This doubles as the staleness threshold the recovery pass uses: a run that finished
    ///         longer ago than this had its chance to drain and did not take it.
    ///     </para>
    /// </summary>
    public static readonly TimeSpan WorkExpectedWithin = HoldWindow + DrainBuffer + TimeSpan.FromMinutes(2);
}
