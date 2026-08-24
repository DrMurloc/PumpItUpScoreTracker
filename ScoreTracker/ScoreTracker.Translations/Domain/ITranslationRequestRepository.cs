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

    /// <summary>Oldest first — starvation-free, which newest-first is not.</summary>
    Task<IReadOnlyList<TranslationWork>> NextIn(TranslationState state, int take,
        CancellationToken cancellationToken = default);

    Task MarkSubmitted(IReadOnlyList<Guid> ids, Guid batchId, TranslationState newState, DateTimeOffset now,
        CancellationToken cancellationToken = default);

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
}
