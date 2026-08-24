namespace ScoreTracker.Translations.Contracts.Events;

/// <summary>
///     One queued text failed for good — a refusal, a malformed response, every rendering losing
///     the marker check. Published so the text's owner can stop promising a translation: a
///     "queued" badge over a text the pipeline has given up on is a lie with no expiry. Retry is
///     the admin's lever, and a successful retry simply publishes
///     <see cref="TextTranslatedEvent" /> like any other completion.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TextTranslationFailedEvent(string SourceKey);
