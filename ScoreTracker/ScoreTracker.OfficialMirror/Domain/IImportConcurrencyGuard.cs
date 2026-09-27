using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

// Stops one user from kicking off overlapping imports — button spam, refresh-and-retry, or a
// second browser tab — and from starting another on a mix within five minutes of the last. In-memory
// by design: the import bus is in-memory too, so the process restart that drops any in-flight import
// also clears the guard (docs/design/import-restart-recovery.md §0).
internal interface IImportConcurrencyGuard
{
    /// <summary>
    ///     Takes the user's slot for an import on <paramref name="mix" />. One import per user at a time,
    ///     whatever the mix. <paramref name="cooldownApplies" /> is false for a deep scan, which the
    ///     monthly allowance already rations; a refused take holds nothing.
    /// </summary>
    ImportSlot TryBegin(Guid userId, MixEnum mix, DateTimeOffset now, bool cooldownApplies);

    /// <summary>
    ///     A run was handed off at <paramref name="at" />: the mix's cooldown starts here, so a press that
    ///     never became a run — a mistyped password, a refusal — starts nothing.
    /// </summary>
    void Started(Guid userId, MixEnum mix, DateTimeOffset at);

    // Releases the slot. Safe to call for a user that never held one. The cooldown outlives it.
    void End(Guid userId);

    /// <summary>
    ///     Whether the user holds a slot for this mix right now — from the press to the run's end. While
    ///     it does, the site and the API refuse the user's other saves on that mix.
    /// </summary>
    bool IsRunning(Guid userId, MixEnum mix);

    /// <summary>
    ///     A deep scan reads every page of a best-score list — around 240 requests for a large
    ///     account — so the site is protected GLOBALLY as well as per user. Three players walking
    ///     their whole history at once is a rude amount of traffic to point at piugame, and the
    ///     per-user slot above says nothing about that.
    /// </summary>
    bool TryBeginDeepScan();

    void EndDeepScan();
}

/// <summary>What asking for the slot came to. <see cref="RetryAfter" /> is set only while cooling down.</summary>
internal readonly record struct ImportSlot(ImportSlotOutcome Outcome, TimeSpan RetryAfter = default)
{
    public static readonly ImportSlot Taken = new(ImportSlotOutcome.Taken);
    public static readonly ImportSlot AlreadyRunning = new(ImportSlotOutcome.AlreadyRunning);
}

internal enum ImportSlotOutcome
{
    Taken = 0,
    AlreadyRunning,
    CoolingDown
}
