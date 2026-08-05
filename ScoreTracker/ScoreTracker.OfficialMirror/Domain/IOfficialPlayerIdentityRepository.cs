using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Player identity on the mirror: the import-confirmed UserId link and the
///     rename-proposal lifecycle. A merge re-points the old player's history onto the
///     new player id and deletes the old dimension row — the proposal row keeps both
///     usernames as the audit trail.
/// </summary>
internal interface IOfficialPlayerIdentityRepository
{
    /// <summary>
    ///     Import-confirmed link: upserts the (mix, username) player and points it at the
    ///     user, overwriting any previous link — the most recent import wins.
    ///     <para>
    ///         Returns the NORMALIZED tag it actually stored. Callers announcing the link must
    ///         quote that rather than what they passed in, or a consumer matching on the tag
    ///         would be matching a spelling this row never used (docs/design/rivals.md D7).
    ///     </para>
    /// </summary>
    Task<string> LinkPlayer(MixEnum mix, string username, Guid userId, DateTimeOffset seenAt,
        CancellationToken ct);

    /// <summary>
    ///     Resolves game tags to mirror players for the supplemented roll-up: a tag the crawl
    ///     has never seen gets a row, and an unlinked row gets its account. An existing
    ///     import-observed link is never overwritten — that one was proved by logging in, this
    ///     one is inferred from a tag the import wrote — and an existing row's LastSeenAt is
    ///     left alone, because that column means "seen on a board", not "we looked it up".
    /// </summary>
    Task<IReadOnlyList<PlayerDimension>> EnsureGameTagLinks(MixEnum mix,
        IReadOnlyCollection<(string Username, Guid UserId)> pairs, DateTimeOffset seenAt, CancellationToken ct);

    /// <summary>Re-points every mirror player linked to one account onto another (account merges).</summary>
    Task RelinkUser(Guid fromUserId, Guid toUserId, CancellationToken ct);

    /// <summary>
    ///     Records what the sweep concluded about every tag that left the boards, and hands
    ///     back what it wrote with ids attached — the conclusive ones are merged immediately,
    ///     through the same accept path an admin uses.
    /// </summary>
    Task<IReadOnlyList<RenameProposal>> WriteFindings(MixEnum mix,
        IReadOnlyCollection<RenameProposal> findings, CancellationToken ct);

    /// <summary>
    ///     The desk. <paramref name="unresolvedOnly" /> false includes what merged itself and
    ///     what was already dismissed, which is the only way to see whether the rule is still
    ///     catching what it should rather than only what it failed to decide.
    /// </summary>
    Task<IReadOnlyList<RenameProposal>> GetFindings(MixEnum mix, bool unresolvedOnly, CancellationToken ct);
    Task<RenameProposal?> GetProposal(int id, CancellationToken ct);
    Task SetProposalStatus(int id, string status, CancellationToken ct);
    Task MergePlayers(int oldPlayerId, int newPlayerId, CancellationToken ct);
}
