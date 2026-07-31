using ScoreTracker.Domain.Records;

namespace ScoreTracker.Identity.Contracts;

public enum AccountDeletionOutcome
{
    Scheduled,

    /// <summary>Already scheduled — the request is idempotent rather than an error.</summary>
    AlreadyScheduled,

    /// <summary>
    ///     The player still creates communities other people are in. They hand each one over or
    ///     delete it first; the system never picks an heir (delete-my-data.md §8.1).
    /// </summary>
    BlockedByOwnedCommunities
}

/// <summary>
///     A refusal is data, not an exception: the blocked case has to render a list of communities
///     with links, and raw exception text is forbidden outside admin pages anyway.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AccountDeletionResult(
    AccountDeletionOutcome Outcome,
    DateTimeOffset? PurgeAfter = null,
    IReadOnlyList<OwnedCommunityRecord>? OwnedCommunities = null)
{
    public IReadOnlyList<OwnedCommunityRecord> Blockers =>
        OwnedCommunities ?? Array.Empty<OwnedCommunityRecord>();
}
