using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Domain;

/// <summary>
///     The community AUDIENCE INDEX over PlayerProgress's significant-win ledger
///     (docs/design/rivals.md D33). One row per (event × community the winner belongs to), holding
///     no wins of its own — the payload lives once, keyed by player, and this says who may see it.
///     <para>
///         The index survives the move because it is what keeps a World-scoped feed a seek: World
///         has every account in it, and joining a member set that size on every read is a
///         different shape of query entirely. A rival list needs no such thing.
///     </para>
///     Vertical-internal — only the index saga, the purge and the feed handler touch it.
/// </summary>
internal interface ICommunityHighlightRepository
{
    /// <summary>
    ///     Index one event against every community the winner belongs to. No-op when the user is in
    ///     no communities, and idempotent on the event id.
    /// </summary>
    Task AddForUserCommunities(Guid eventId, Guid userId, MixEnum mix, DateTimeOffset occurredAt,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Event ids visible to the requester across the named communities, newest first, deduped —
    ///     a win in several shared crews is one row, not several. Gated on the requester's own
    ///     membership (the consent boundary, CH2): a community they aren't in yields nothing even
    ///     if named.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetVisibleEventIds(Guid requestingUserId,
        IReadOnlyCollection<Name> communityNames, MixEnum mix, int take, CancellationToken cancellationToken);

    /// <summary>Drop index rows older than the cutoff. Returns rows removed.</summary>
    Task<int> PurgeBefore(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
