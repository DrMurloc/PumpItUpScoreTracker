namespace ScoreTracker.Identity.Contracts;

/// <summary>A deletion the player has asked for and can still cancel.</summary>
[ExcludeFromCodeCoverage]
public sealed record PendingAccountDeletion(DateTimeOffset RequestedAt, DateTimeOffset PurgeAfter);
