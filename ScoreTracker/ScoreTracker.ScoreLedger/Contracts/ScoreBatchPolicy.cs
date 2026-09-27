using ScoreTracker.SharedKernel.Enums;

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
    ///     it, not the first. Every score pushes the deadline out again, so a player typing in a set
    ///     of scores gets one announcement two minutes after the last. Only typed entries ride the
    ///     batch: an import announces the moment its last score saves, and a CSV upload drains it
    ///     when the upload ends (docs/design/import-restart-recovery.md §0).
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
    ///         hold window is gone, and an import the restart cut short does not announce until the
    ///         next process start replays it (docs/design/import-restart-recovery.md §0). Either way
    ///         a reader that stops waiting is simply early, which is why nothing may treat the end of
    ///         this as proof that there was nothing to wait for.
    ///     </para>
    /// </summary>
    public static readonly TimeSpan WorkExpectedWithin = HoldWindow + DrainBuffer + TimeSpan.FromMinutes(2);

    // The sweep job's own cadence (docs/SCHEDULED-JOBS.md, flush-overdue-score-batches).
    private static readonly TimeSpan SweepTick = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     The gap that ends a sitting on this mix — the plays the plays endpoint records. A play joins
    ///     an open sitting when it was played within this of the sitting's plays, and a sitting closes
    ///     and announces itself once this long passes with no play arriving (docs/design/rise.md §12).
    ///     The mix's profile holds it (<see cref="MixProfile.SittingWindow" />), so a mix whose players
    ///     sit longer says so there rather than in a check here.
    /// </summary>
    public static TimeSpan SittingQuietWindow(MixEnum mix)
    {
        return MixProfiles.For(mix).SittingWindow;
    }

    /// <summary>
    ///     How long past its last arrival an unannounced sitting on this mix waits before the sweep
    ///     closes it: the mix's quiet window plus the sweep's own five-minute tick, so the scheduled
    ///     close normally gets there first and the sweep only catches one that was lost.
    /// </summary>
    public static TimeSpan SittingOverdueAfter(MixEnum mix)
    {
        return SittingQuietWindow(mix) + SweepTick;
    }

    /// <summary>
    ///     A sitting whose last play is older than this when it closes — a backlog a tool sent late —
    ///     records and captures as usual but posts no Discord card, so an old backlog cannot flood a
    ///     channel (docs/design/rise.md D23).
    /// </summary>
    public static readonly TimeSpan SittingCardCutoff = TimeSpan.FromDays(1);
}
