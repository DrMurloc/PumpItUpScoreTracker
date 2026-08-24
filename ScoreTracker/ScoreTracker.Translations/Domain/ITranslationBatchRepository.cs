using ScoreTracker.Domain.Records;

namespace ScoreTracker.Translations.Domain;

/// <summary>One submitted provider batch — the pipeline's spend ledger as well as its poll list.</summary>
internal sealed record TranslationBatchInfo(
    Guid Id,
    string ProviderBatchId,
    TranslationState Stage,
    DateTimeOffset SubmittedAt);

internal interface ITranslationBatchRepository
{
    Task Record(TranslationBatchInfo batch, int itemCount, CancellationToken cancellationToken = default);

    /// <summary>Batches submitted but not yet collected.</summary>
    Task<IReadOnlyList<TranslationBatchInfo>> Open(CancellationToken cancellationToken = default);

    Task Complete(Guid id, LanguageModelUsage totalUsage, decimal costUsd, DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Actual dollars from completed batches since the cutoff — the rolling ceiling's ledger.</summary>
    Task<decimal> SpendSince(DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> LastSubmittedAt(CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> LastCollectedAt(CancellationToken cancellationToken = default);
}
