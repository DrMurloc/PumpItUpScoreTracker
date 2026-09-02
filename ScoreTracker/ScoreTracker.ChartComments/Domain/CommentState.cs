using ScoreTracker.ChartComments.Contracts;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Everything a <see cref="Comment" /> is made of, in one parameter.
///     <para>
///         A comment carries twelve fields, and passing twelve positional arguments is how a
///         <c>DeletedAt</c> ends up where a <c>CreatedAt</c> belongs — both nullable
///         <see cref="DateTimeOffset" />, both silently accepted. Naming them at the call site
///         costs one type and makes that class of mistake a compiler error.
///     </para>
///     <para>
///         The trailing five default because a new comment has none of them: nothing is edited,
///         deleted, written in a language anybody has detected yet, or pointed at a second of the
///         chart unless the author said so.
///     </para>
/// </summary>
internal sealed record CommentState(
    Guid Id,
    Guid ChartId,
    Guid UserId,
    CommentAudience Audience,
    Guid? ParentCommentId,
    string Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt = null,
    DateTimeOffset? DeletedAt = null,
    Guid? DeletedByUserId = null,
    string? SourceLanguage = null,
    DateTimeOffset? TranslationQueuedAt = null,
    decimal? AnchorAt = null);
