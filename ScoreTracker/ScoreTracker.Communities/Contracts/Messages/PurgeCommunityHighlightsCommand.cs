namespace ScoreTracker.Communities.Contracts.Messages;

/// <summary>
///     Hangfire trigger (weekly): drop community-highlight summaries past the 30-day retention
///     window (CH7). Imperative by design — a bus trigger, not a past-tense event.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PurgeCommunityHighlightsCommand;
