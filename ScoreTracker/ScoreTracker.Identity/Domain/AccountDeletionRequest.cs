namespace ScoreTracker.Identity.Domain;

/// <summary>
///     The durable record of a self-serve account deletion: when it was asked for, when the
///     purge may begin, and what the account looked like before it was hidden.
/// </summary>
internal sealed record AccountDeletionRequest(
    Guid Id,
    Guid UserId,
    DateTimeOffset RequestedAt,
    DateTimeOffset PurgeAfter,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? PurgedAt,
    bool WasPublic,
    string? GameTag)
{
    public bool IsPending => CancelledAt == null && PurgedAt == null;
}
