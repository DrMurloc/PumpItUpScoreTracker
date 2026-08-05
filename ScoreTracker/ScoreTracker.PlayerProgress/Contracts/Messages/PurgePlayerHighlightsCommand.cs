namespace ScoreTracker.PlayerProgress.Contracts.Messages;

/// <summary>
///     Hangfire trigger (weekly): drop significant-win summaries past the 30-day retention
///     window (CH7). Imperative by design — a bus trigger, not a past-tense event.
///     <para>
///         TWO consumers: PlayerProgress drops the payloads, Communities drops its audience index
///         rows. One command rather than two so the pair can never fall out of step and leave an
///         index pointing at expired wins.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PurgePlayerHighlightsCommand;
