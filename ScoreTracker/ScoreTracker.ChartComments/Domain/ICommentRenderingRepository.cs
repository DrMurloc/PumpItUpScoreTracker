namespace ScoreTracker.ChartComments.Domain;

/// <summary>One stored rendering, links already substituted back — parseable like any body.</summary>
internal sealed record CommentRenderingRow(Guid CommentId, string Locale, string Text, string TranslatedBy);

/// <summary>
///     The renderings a comment's translation produced, and the one write that lands them.
///     Renderings are derived data: they die with their comment and are cleared by an edit, so
///     nothing here updates in place — a translation replaces the set wholesale.
/// </summary>
internal interface ICommentRenderingRepository
{
    /// <summary>
    ///     Replaces the comment's renderings and stamps its detected source language in the same
    ///     write — the two answers arrive together on <c>TextTranslatedEvent</c> and displaying
    ///     one without the other mis-resolves rule one.
    /// </summary>
    Task StoreTranslation(Guid commentId, string sourceLanguage,
        IReadOnlyDictionary<string, string> renderings, string translatedBy, DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommentRenderingRow>> GetFor(IReadOnlyList<Guid> commentIds,
        CancellationToken cancellationToken = default);

    Task<bool> AnyFor(Guid commentId, CancellationToken cancellationToken = default);

    /// <summary>An edit or a delete: the text these rendered no longer exists.</summary>
    Task DeleteFor(Guid commentId, CancellationToken cancellationToken = default);
}
