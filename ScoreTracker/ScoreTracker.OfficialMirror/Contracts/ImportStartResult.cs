namespace ScoreTracker.OfficialMirror.Contracts;

// The synchronous outcome of kicking off an import. Started means the scrape is now running in
// the background; the rest are pre-flight failures the UI reflects immediately. RetryAfter is set only
// on CoolingDown: how long until this mix can be imported again.
[ExcludeFromCodeCoverage]
public sealed record ImportStartResult(ImportStartOutcome Outcome, TimeSpan? RetryAfter = null);

public enum ImportStartOutcome
{
    Started,
    CredentialUnlockFailed,
    InvalidCredentials,

    // This user already has an import in flight — don't kick off a second scrape of the site.
    AlreadyRunning,

    // An import started on this mix less than five minutes ago. Refused before piugame is touched.
    CoolingDown
}
