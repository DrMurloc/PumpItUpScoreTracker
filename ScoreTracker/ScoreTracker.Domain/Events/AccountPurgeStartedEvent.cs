namespace ScoreTracker.Domain.Events;

/// <summary>
///     A retired account's merge grace window ended: every vertical that holds rows keyed by
///     this user deletes its own (cross-vertical SQL stays forbidden). Consumers must be
///     idempotent — the trigger re-fires daily for a week past the purge date so a process
///     death mid-purge self-heals (the bus is in-memory). Lives in Domain/Events because every
///     vertical consumes it; Identity publishes it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AccountPurgeStartedEvent(Guid RetiredUserId)
{
}
