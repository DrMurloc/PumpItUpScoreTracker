namespace ScoreTracker.Identity.Contracts.Events;

/// <summary>
///     A retired account's grace window ended: every vertical that holds rows keyed by this
///     user must delete them. Consumers must be idempotent — the event re-fires daily for a
///     week so a process death mid-purge self-heals (the bus is in-memory).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AccountPurgeStartedEvent(Guid RetiredUserId)
{
}
