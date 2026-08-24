namespace ScoreTracker.Translations.Contracts.Messages;

/// <summary>
///     Queues one piece of community text for the nightly translation pipeline.
///     <para>
///         <paramref name="SourceKey" /> is opaque to this vertical — it is echoed back on
///         <see cref="Events.TextTranslatedEvent" /> and never parsed, which is what lets any
///         owner of text (comments today, community descriptions or tool blurbs someday) ride the
///         same pipeline. A re-queue for a key that is already waiting <b>replaces</b> its row;
///         the pipeline keeps no history.
///     </para>
///     <para>
///         The text arrives with links already lifted to <see cref="TranslationMarkers" /> —
///         extraction and substitution belong to the caller, because the caller owns the parser
///         that defines what a link is. The pipeline promises the markers survive translation
///         verbatim and discards any rendering that mishandles one.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueueTextForTranslationCommand(string SourceKey, string Text);

/// <summary>
///     Drops queued work and stored pivots for these source keys — published when the text they
///     came from stops existing (a purged account, an archived community, a hard-deleted comment).
///     The pipeline must not hold text whose original is gone, and must not spend money on it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DiscardTranslationRequestsCommand(IReadOnlyList<string> SourceKeys);

/// <summary>
///     Builds and submits both stages' batches — pending texts into the pivot stage, pivoted ones
///     into the fan-out — under the spend ceiling and the nightly count. Published nightly by the
///     recurring job, and by the admin page's Drain now, which is deliberately the same path.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SubmitTranslationBatchesCommand;

/// <summary>
///     Polls open batches, writes finished results, advances the state machine, and records
///     usage. Published hourly; a batch usually lands within the hour and always within a day.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CollectTranslationBatchesCommand;
