namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>How a v1 import ended. <see cref="RetryAfter" /> is set only on CoolingDown.</summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialImportResult(OfficialImportOutcome Outcome, TimeSpan? RetryAfter = null);

public enum OfficialImportOutcome
{
    Imported,
    GameTagNotFound,
    AlreadyRunning,
    CoolingDown
}
