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
    int TakeRoots = 20,
    /// <summary>
    ///     The locale the reader browses the site in — display resolution judges by its language,
    ///     region ignored. Null (a caller that has not adopted translation display) reads as
    ///     originals-only.
    /// </summary>
    string? ReaderLocale = null,
    /// <summary>
    ///     A localization the reader picked by hand, remembered in UiSettings by Web and passed
    ///     through. Replaces the language-mapping step only: a comment in the reader's own
    ///     language stays the original even against a stored pick.
    /// </summary>
    string? PreferredLocale = null) : IQuery<CommentPageRecord>;

/// <summary>
///     What the step chart draws and its panel reads (docs/design/step-chart-comments): the
///     anchored, living roots of one scope <b>plus the reader's own anchored notes</b> (D7), in
///     chart order, unpaged — the strip is a map, and a map with the far end cut off is a lie.
///     Replies ride along for their count; bodies resolve for display exactly as the tab's do.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartCommentMarksQuery(
    Guid ChartId,
    CommentAudience Audience,
    string? ReaderLocale = null,
    string? PreferredLocale = null) : IQuery<IReadOnlyList<CommentRecord>>;

/// <summary>
///     How many anchored, living roots each of the reader's scopes holds on one chart — what
///     decides whether the strip's scope filter renders at all, and which scopes it lists
///     (docs/design/step-chart-comments D18). Signed out, the answer is Public alone.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartCommentScopeCountsQuery(Guid ChartId) : IQuery<IReadOnlyList<CommentScopeCountRecord>>;

[ExcludeFromCodeCoverage]
public sealed record CommentScopeCountRecord(CommentAudience Audience, int AnchoredComments);

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
