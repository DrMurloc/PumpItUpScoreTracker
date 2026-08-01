using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     The communities standing between this player and deleting their account, asked before
///     they type anything — nobody should confirm a username only to be told no.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetAccountDeletionBlockersQuery(Guid UserId)
    : IQuery<IReadOnlyList<OwnedCommunityRecord>>;
