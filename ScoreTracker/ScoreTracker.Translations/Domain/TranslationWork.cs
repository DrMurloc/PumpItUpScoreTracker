namespace ScoreTracker.Translations.Domain;

/// <summary>
///     The four-state machine per text, plus the two ways out. Submit moves work right; Collect
///     moves it right or to Failed; a re-queue for the same source key resets it to Pending.
/// </summary>
internal enum TranslationState
{
    Pending,
    PivotSubmitted,
    PivotDone,
    FanOutSubmitted,
    Translated,
    Failed
}

/// <summary>
///     One queued text as the pipeline works it. <see cref="Id" /> doubles as the batch custom id
///     (in "N" form — the provider's custom-id alphabet has no colon, so the opaque
///     <see cref="SourceKey" /> cannot be used directly). <see cref="PivotJson" /> is the stored
///     stage-one output — the owner's call: kept for debugging and locale backfill, never
///     displayed.
/// </summary>
internal sealed record TranslationWork(
    Guid Id,
    string SourceKey,
    string Text,
    TranslationState State,
    string? SourceLanguage,
    string? PivotJson,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSubmittedAt = null);
