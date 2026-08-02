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

    /// <summary>
    ///     The backfill's source: rows written before the capture moved out, which still carry a
    ///     payload. One row per event — the fan-out wrote the same payload to each of the winner's
    ///     communities, so the extra copies are noise. Rows written since the move are skipped
    ///     because their payload column is empty.
    /// </summary>
    Task<IReadOnlyList<LegacyHighlightPayload>> GetLegacyPayloads(CancellationToken cancellationToken);
}

/// <summary>One pre-move row, still carrying the JSON the ledger now wants.</summary>
internal sealed record LegacyHighlightPayload(
    Guid EventId,
    Guid UserId,
    MixEnum Mix,
    DateTimeOffset OccurredAt,
    Guid? SessionId,
    string Payload,
    int SchemaVersion);
