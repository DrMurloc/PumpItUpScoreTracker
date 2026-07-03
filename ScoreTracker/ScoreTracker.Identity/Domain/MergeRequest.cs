using ScoreTracker.Domain.Records;

namespace ScoreTracker.Identity.Domain;

internal enum MergeState
{
    Active,
    Undone,
    Purging,
    Purged
}

/// <summary>
///     The durable record of an account merge: who survived, who was retired, which logins
///     moved (for undo), what the retired account looked like (for undo), and when its data
///     becomes purgeable. This record is what makes the purge re-fireable after a crash —
///     the in-memory bus loses in-flight messages with the process.
/// </summary>
internal sealed record MergeRequest(
    Guid Id,
    Guid SurvivorUserId,
    Guid RetiredUserId,
    IReadOnlyList<ExternalLoginRecord> MovedLogins,
    RetiredUserSnapshot Snapshot,
    MergeState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset PurgeAfter,
    DateTimeOffset? PurgedAt);

internal sealed record RetiredUserSnapshot(bool IsPublic, string? GameTag);
