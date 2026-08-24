using ScoreTracker.ChartComments.Contracts;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     One chart's rows for one audience, plus the writes. Vertical-internal: nothing outside
///     ChartComments reads these tables, and nothing outside it gets to choose an audience
///     predicate.
/// </summary>
internal interface ICommentRepository
{
    /// <summary>
    ///     The rows a reader may see for one chart and one audience, roots first with their replies
    ///     attached.
    ///     <para>
    ///         ⚠ <paramref name="viewerId" /> is not a convenience for the vote flags — it is the
    ///         audience gate. A private audience returns only that viewer's own notes, which is why
    ///         this predicate lives here and not in a handler somebody could write a second copy of.
    ///     </para>
    /// </summary>
    Task<IReadOnlyList<CommentRow>> GetForChart(Guid chartId, CommentAudience audience, Guid viewerId,
        CommentSort sort, int takeRoots, CancellationToken cancellationToken = default);

    /// <summary>How many roots exist beyond the ones returned, so the UI knows whether to offer more.</summary>
    Task<int> CountRoots(Guid chartId, CommentAudience audience, Guid viewerId,
        CancellationToken cancellationToken = default);

    Task<Comment?> GetById(Guid commentId, CancellationToken cancellationToken = default);

    Task<bool> HasReplies(Guid commentId, CancellationToken cancellationToken = default);

    Task Save(Comment comment, CancellationToken cancellationToken = default);

    /// <summary>Records the body an edit replaced. Retained for moderation.</summary>
    Task WriteRevision(Guid commentId, string replacedText, DateTimeOffset replacedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Idempotent: the unique index is what makes a double-tap one vote.</summary>
    Task AddVote(Guid commentId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task RemoveVote(Guid commentId, Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
///     A stored row plus the two counts the reader's own identity decides. Internal: the contract
///     record is assembled from this once the body has been parsed and its links judged.
/// </summary>
internal sealed record CommentRow(
    Guid Id,
    Guid ChartId,
    Guid UserId,
    Guid? ParentCommentId,
    string Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    DateTimeOffset? DeletedAt,
    Guid? DeletedByUserId,
    int Votes,
    bool ViewerVoted,
    string? SourceLanguage = null,
    DateTimeOffset? TranslationQueuedAt = null);
