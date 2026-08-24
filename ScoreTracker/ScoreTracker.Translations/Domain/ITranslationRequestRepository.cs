namespace ScoreTracker.Translations.Domain;

/// <summary>
///     The queue. One row per source key — an upsert replaces whatever was there, because the
///     pipeline translates what a text says now, not what it said when first queued.
/// </summary>
internal interface ITranslationRequestRepository
{
    Task Upsert(string sourceKey, string text, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Removes rows whose originals stopped existing, whatever state they were in.</summary>
    Task Discard(IReadOnlyList<string> sourceKeys, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Oldest first — starvation-free, which newest-first is not. A non-null
    ///     <paramref name="notSubmittedSince" /> is the submit-side cooldown: rows whose
    ///     <c>LastSubmittedAt</c> is on or after it wait for a later night, which is what makes
    ///     "a text translates at most once per 24 h" true however often its author edits.
    /// </summary>
    Task<IReadOnlyList<TranslationWork>> NextIn(TranslationState state, int take,
        DateTimeOffset? notSubmittedSince = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Guarded per row on <see cref="TranslationWork.UpdatedAt" />: a row an edit re-queued
    ///     between the read and this mark is left alone — its batch result will find no BatchId
    ///     pointing here and be ignored, which is the cheap side of that race. Returns the rows
    ///     actually marked.
    /// </summary>
    Task<IReadOnlyList<Guid>> MarkSubmitted(IReadOnlyList<TranslationWork> works, Guid batchId,
        TranslationState newState, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TranslationWork>> InBatch(Guid batchId, CancellationToken cancellationToken = default);

    Task CompletePivot(Guid id, string sourceLanguage, string pivotJson, DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task CompleteTranslation(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task Fail(Guid id, string reason, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<int> CountIn(TranslationState state, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> OldestPendingCreatedAt(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TranslationWork>> RecentFailures(int take, CancellationToken cancellationToken = default);

    /// <summary>Every translated row back to Pending — the re-translation sweep. Returns the count.</summary>
    Task<int> RequeueTranslated(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Every failed row back to Pending — the admin's retry lever. Returns the count.</summary>
    Task<int> RequeueFailed(DateTimeOffset now, CancellationToken cancellationToken = default);
}
