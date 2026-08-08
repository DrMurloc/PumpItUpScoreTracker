namespace ScoreTracker.ChartComments.Contracts.Queries;

/// <summary>
///     One chart's comments for one scope. <paramref name="TakeRoots" /> bounds the first render —
///     replies are never truncated, because a conversation missing its answer is worse than a long
///     page.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartCommentsQuery(
    Guid ChartId,
    CommentAudience Audience,
    CommentSort Sort = CommentSort.Top,
    int TakeRoots = 20) : IQuery<CommentPageRecord>;

/// <summary>
///     The scope rail: Public, Notes, then the reader's non-regional communities. Empty for a
///     signed-out reader, who may read public comments but has nowhere of their own to post.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyCommentScopesQuery : IQuery<IReadOnlyList<CommentScopeRecord>>;

/// <summary>
///     What the reader still has to agree to before the audience they are standing in will take a
///     comment. Both false means the composer opens straight into a text field.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommentConsentQuery(CommentAudience Audience) : IQuery<CommentConsentRecord>;

[ExcludeFromCodeCoverage]
public sealed record CommentConsentRecord(bool NeedsTerms, bool NeedsPublicIdentityConsent)
{
    public bool NeedsAnything => NeedsTerms || NeedsPublicIdentityConsent;
}

/// <summary>
///     The raw body of a comment, so its author can edit it. Null for anyone else — this is the
///     one place raw comment text crosses the boundary, and it is gated on being your own words.
///     <para>
///         Deliberately a separate query rather than a field on <see cref="CommentRecord" />: the
///         render contract carries spans and nothing else, so the only string Web ever holds is one
///         it asked for by name and is about to put in a textarea.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyCommentTextQuery(Guid CommentId) : IQuery<string?>;
