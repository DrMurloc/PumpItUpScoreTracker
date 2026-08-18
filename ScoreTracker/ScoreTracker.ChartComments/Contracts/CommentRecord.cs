using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartComments.Contracts;

/// <summary>
///     One comment as a reader sees it, with its replies attached and every question about what the
///     reader may do already answered. Web renders this and decides nothing.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommentRecord(
    Guid Id,
    Guid ChartId,
    /// <summary>Null on a stub — the author is gone, deleted, or removed.</summary>
    Guid? AuthorId,
    Name? AuthorName,
    Name? AuthorCountry,
    Uri? AuthorImage,
    /// <summary>The parsed body. Empty on a stub. Never a string, so Web has no raw text to mishandle.</summary>
    IReadOnlyList<CommentSpan> Body,
    int Votes,
    bool ViewerVoted,
    bool ViewerIsAuthor,
    /// <summary>Whether to draw the shield. Its presence is the permission.</summary>
    bool ViewerMayModerate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    /// <summary>Null unless this row is a stub; the value is which of the three wordings to use.</summary>
    CommentDeletion? Deletion,
    IReadOnlyList<CommentRecord> Replies);

/// <summary>
///     A chart's comments for one scope, plus whether there are more roots behind the twenty
///     returned.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommentPageRecord(
    IReadOnlyList<CommentRecord> Roots,
    int TotalRoots,
    bool HasMore);

/// <summary>
///     One chip on the scope rail: an audience the reader may read, and — when
///     <see cref="CanPost" /> holds — post to. False means the chip stays (reading is never
///     revoked) and the composer renders disabled: a mute in that club, or the account lock
///     anywhere public. Notes are always postable — a note has no audience to protect.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommentScopeRecord(CommentAudience Audience, Name Label, bool CanPost = true);
