using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     The player's pending deletion, if any. Read by the sign-in notice, the account banner,
///     and both Communities guards — they consult it rather than copying a flag, so cancelling
///     lifts them on its own.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPendingAccountDeletionQuery(Guid UserId) : IQuery<PendingAccountDeletion?>;
