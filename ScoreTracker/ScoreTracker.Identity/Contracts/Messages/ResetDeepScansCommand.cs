namespace ScoreTracker.Identity.Contracts.Messages;

// Bus trigger: refill every account's deep-scan balance. Fired monthly by Hangfire.
[ExcludeFromCodeCoverage]
public sealed record ResetDeepScansCommand;
