namespace ScoreTracker.Rivals.Domain;

/// <summary>
///     The rival graph (docs/design/rivals.md §2.1). Vertical-internal: nothing outside Rivals
///     reads an edge except through the published contract queries.
///     <para>
///         Blocks live here rather than in their own port because blocking and un-rivalling are
///         one transaction — a block that left the edges standing would be a setting, not a block.
///     </para>
/// </summary>
internal interface IRivalRepository
{
    /// <summary>Every arrow this user drew, newest first.</summary>
    Task<IReadOnlyList<RivalEdge>> GetRivalsOwnedBy(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>
    ///     Every arrow pointing AT this user — the reverse list, which is the only revocation the
    ///     system has (D14), so it must never omit a row for being inconvenient.
    /// </summary>
    Task<IReadOnlyList<RivalEdge>> GetRivalsTargeting(Guid targetUserId, CancellationToken cancellationToken);

    Task<RivalEdge?> GetEdge(Guid edgeId, CancellationToken cancellationToken);

    /// <summary>True when this owner already points at this target, in either target shape.</summary>
    Task<bool> EdgeExists(Guid ownerUserId, Guid? targetUserId, string? targetTag,
        CancellationToken cancellationToken);

    Task Add(RivalEdge edge, CancellationToken cancellationToken);

    /// <summary>Drops one edge. Returns false when it was already gone (a double-click, a re-send).</summary>
    Task<bool> Remove(Guid edgeId, CancellationToken cancellationToken);

    /// <summary>
    ///     A block in either direction. The add path asks this before anything else, so neither
    ///     party can re-form the arrow the block dissolved.
    /// </summary>
    Task<bool> IsBlockedEitherWay(Guid userId, Guid otherUserId, CancellationToken cancellationToken);

    /// <summary>
    ///     Writes the block and deletes both users' edges onto each other, in one transaction.
    ///     Idempotent: blocking twice is the same as blocking once.
    /// </summary>
    Task Block(Guid userId, Guid blockedUserId, DateTimeOffset at, CancellationToken cancellationToken);

    Task Unblock(Guid userId, Guid blockedUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RivalBlockRecord>> GetBlockedBy(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    ///     The ghost-becomes-real step (D5). Rewrites every edge pointing at <paramref name="tag" />
    ///     to point at the account instead, dropping any that would collide with an edge the owner
    ///     already holds on that user. Returns how many were promoted.
    /// </summary>
    Task<int> PromoteTagToUser(string tag, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    ///     Follows an accepted rename (D5). Edges whose owner already points at the new tag are
    ///     dropped rather than duplicated. Returns how many were rewritten.
    /// </summary>
    Task<int> RenameTag(string oldTag, string newTag, CancellationToken cancellationToken);
}

/// <summary>
///     One stored arrow. Exactly one of <paramref name="TargetUserId" /> /
///     <paramref name="TargetTag" /> is set.
/// </summary>
internal sealed record RivalEdge(
    Guid Id,
    Guid OwnerUserId,
    Guid? TargetUserId,
    string? TargetTag,
    DateTimeOffset AddedAt);

internal sealed record RivalBlockRecord(Guid BlockedUserId, DateTimeOffset CreatedAt);
