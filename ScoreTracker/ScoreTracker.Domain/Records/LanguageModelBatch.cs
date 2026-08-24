namespace ScoreTracker.Domain.Records;

/// <summary>
///     One request inside a batch. <paramref name="CustomId" /> is the caller's correlation key —
///     the only thing that connects a result back to what asked for it, because results return
///     unordered.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LanguageModelBatchItem(string CustomId, LanguageModelRequest Request);

/// <summary>
///     Where a batch stands. <paramref name="HasEnded" /> is the one gate that matters — results
///     are only readable after it — and the counts are for reporting, not control flow.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LanguageModelBatchStatus(
    string BatchId,
    bool HasEnded,
    long Succeeded,
    long Errored,
    long Expired,
    long Canceled,
    long Processing);

/// <summary>
///     One item's outcome. Exactly one of the two halves is set: a <paramref name="Response" />
///     when the model answered, an <paramref name="Error" /> when it did not — including a refusal,
///     which is an error *for that item* rather than for the batch around it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LanguageModelBatchResult(
    string CustomId,
    LanguageModelResponse? Response,
    string? Error = null);
