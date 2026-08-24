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
    IReadOnlyList<CommentRecord> Replies,
    /// <summary>Null on a stub and on a personal note — neither is ever translated.</summary>
    CommentTranslationRecord? Translation = null);

/// <summary>
///     How <see cref="CommentRecord.Body" /> was resolved for this reader, and what else they may
///     flip to. The resolution itself happened inside the vertical (own language → original;
///     else the reader's language's rendering; else the original) — Web renders what it is handed
///     and decides nothing.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommentTranslationRecord(
    /// <summary>Primary subtag the author wrote in — null until the pipeline detects it.</summary>
    string? SourceLanguage,
    /// <summary>True when Body is a rendering: badge it and offer Show original.</summary>
    bool BodyIsTranslated,
    /// <summary>The locale Body was rendered in, when it was.</summary>
    string? BodyLocale,
    /// <summary>The author's own words, for the transient Show original flip. Empty when Body already is them.</summary>
    IReadOnlyList<CommentSpan> OriginalBody,
    /// <summary>Renderings that exist for this comment — the Read-in picker's vocabulary.</summary>
    IReadOnlyList<string> AvailableLocales,
    /// <summary>True only for a reader whose default would be a rendering that does not exist yet.</summary>
    bool Pending);

/// <summary>
///     A chart's comments for one scope, plus whether there are more roots behind the twenty
///     returned.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommentPageRecord(
    IReadOnlyList<CommentRecord> Roots,
    int TotalRoots,
    bool HasMore,
    /// <summary>
    ///     Whether the translation pipeline is armed for this scope — what tells the UI to offer
    ///     the Read-in picker even on a page whose renderings have not arrived yet. A reader whose
    ///     language never renders (Italian, Japanese) picks a localization here BEFORE anything is
    ///     translated; that is the picker's whole audience. Always false for Notes.
    /// </summary>
    bool TranslationOffered = false);

/// <summary>
///     One chip on the scope rail: an audience the reader may read, and — when
///     <see cref="CanPost" /> holds — post to. False means the chip stays (reading is never
///     revoked) and the composer renders disabled: a mute in that club, or the account lock
///     anywhere public. Notes are always postable — a note has no audience to protect.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommentScopeRecord(CommentAudience Audience, Name Label, bool CanPost = true);
