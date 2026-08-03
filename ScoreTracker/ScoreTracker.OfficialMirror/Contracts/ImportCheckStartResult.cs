namespace ScoreTracker.OfficialMirror.Contracts;

// The synchronous outcome of kicking off a check. Started means it is now running in the
// background; the rest are pre-flight refusals the panel reflects immediately.
[ExcludeFromCodeCoverage]
public sealed record ImportCheckStartResult(ImportCheckStartOutcome Outcome, int DeepScansLeft = 0);

public enum ImportCheckStartOutcome
{
    Started,
    CredentialUnlockFailed,
    InvalidCredentials,
    AlreadyRunning,

    /// <summary>This month's three deep scans are spent. The census is still free.</summary>
    NoDeepScansLeft,

    /// <summary>Another deep scan is already walking piugame. Ours waits rather than piling on.</summary>
    DeepScanQueueFull
}
