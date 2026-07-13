namespace ScoreTracker.OfficialMirror.Contracts;

// The synchronous outcome of kicking off an import. Started means the scrape is now running in
// the background; the rest are pre-flight failures the UI reflects immediately.
[ExcludeFromCodeCoverage]
public sealed record ImportStartResult(ImportStartOutcome Outcome);

public enum ImportStartOutcome
{
    Started,
    CredentialUnlockFailed,
    InvalidCredentials
}
