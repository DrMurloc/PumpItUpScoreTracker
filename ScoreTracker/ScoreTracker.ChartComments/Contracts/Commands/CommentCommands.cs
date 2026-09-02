using MediatR;

namespace ScoreTracker.ChartComments.Contracts.Commands;

/// <summary>
///     Posts a root comment or a personal note. A reply uses <see cref="ReplyToCommentCommand" />,
///     which takes no audience at all — that is the invariant, expressed in the contract.
/// </summary>
/// <remarks>
///     <paramref name="AnchorAt" /> is the second of the chart the comment points at
///     (docs/design/step-chart-comments D1), already snapped to the nearest arrow row by the
///     client that has the rows. Null is a comment about the whole chart.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record PostCommentCommand(Guid ChartId, CommentAudience Audience, string Text,
    decimal? AnchorAt = null)
    : IRequest<Guid>;

/// <summary>
///     Answers a comment. The audience comes from the root, which is why it is absent here; if
///     <paramref name="ParentCommentId" /> names a reply, the root it belongs to is used instead.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ReplyToCommentCommand(Guid ParentCommentId, string Text) : IRequest<Guid>;

[ExcludeFromCodeCoverage]
public sealed record EditCommentCommand(Guid CommentId, string Text) : IRequest;

/// <summary>The author removing their own. Soft, so a thread that answered it keeps its shape.</summary>
[ExcludeFromCodeCoverage]
public sealed record DeleteCommentCommand(Guid CommentId) : IRequest;

/// <summary>
///     A moderator taking something down. Remove and only remove — there is deliberately no
///     moderator equivalent of <see cref="EditCommentCommand" />.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RemoveCommentCommand(Guid CommentId) : IRequest;

/// <summary>Toggles the reader's thumbs-up. Never on their own, never on a note.</summary>
[ExcludeFromCodeCoverage]
public sealed record VoteOnCommentCommand(Guid CommentId, bool Voted) : IRequest;

/// <summary>
///     Records the rules-card agreement. <paramref name="ConsentedToPublicIdentity" /> is set only
///     when it is actually true — a private-profile player posting in public — rather than
///     collected in advance from everyone.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AcceptCommentTermsCommand(bool ConsentedToPublicIdentity) : IRequest;
