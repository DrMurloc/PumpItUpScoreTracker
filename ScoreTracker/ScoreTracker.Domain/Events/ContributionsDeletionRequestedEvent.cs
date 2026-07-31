using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.Events;

/// <summary>
///     A player asked for their contributions to go. Each vertical deletes its own rows in its
///     own consumer, exactly as the account purge does — no vertical learns that another exists,
///     and reaching four of them synchronously would cost either cross-vertical references or
///     orchestration logic in the UI (docs/design/delete-my-data.md §14).
///     Unlike the score wipe this is eventual: it is the rarer path, and the honest answer to
///     the player is "removing these" rather than "removed".
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ContributionsDeletionRequestedEvent(Guid UserId, ContributionDeletionItems Items);
