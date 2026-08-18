namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Community mutes. Vertical-internal, like every ChartComments port. Lifted rows are
///     retained — a mute's history is how "why can't I post" gets answered months later — so
///     every read here filters to active.
/// </summary>
internal interface ICommentRestrictionRepository
{
    Task Save(CommentRestriction restriction, CancellationToken cancellationToken = default);

    /// <summary>The active mute for one user in one community, if any.</summary>
    Task<CommentRestriction?> GetActive(Guid userId, Guid communityId,
        CancellationToken cancellationToken = default);

    /// <summary>Every active mute in one community — the Members page's lift surface.</summary>
    Task<IReadOnlyList<CommentRestriction>> GetActiveForCommunity(Guid communityId,
        CancellationToken cancellationToken = default);

    /// <summary>Every community where this user is actively muted — the scope rail's CanPost read.</summary>
    Task<IReadOnlyList<CommentRestriction>> GetActiveForUser(Guid userId,
        CancellationToken cancellationToken = default);
}
