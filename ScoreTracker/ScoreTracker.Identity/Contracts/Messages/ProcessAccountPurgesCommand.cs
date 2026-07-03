namespace ScoreTracker.Identity.Contracts.Messages;

/// <summary>
///     Hangfire trigger (daily): find merges past their grace window and fire the purge.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessAccountPurgesCommand
{
}
