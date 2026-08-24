namespace ScoreTracker.Translations.Contracts.Events;

/// <summary>
///     One queued text finished translating. <paramref name="SourceKey" /> is the caller's own
///     key, echoed back untouched.
///     <para>
///         <paramref name="Translations" /> is keyed by rendering locale and deliberately sparse:
///         the locale that speaks the text's own language is absent — rendering it would replace
///         the author's words with a paraphrase — and <b>absence is the instruction to show the
///         original</b>. When the source is not English, the en-US pivot is included as a
///         rendering like any other. Texts still carry their <see cref="TranslationMarkers" />;
///         the consumer substitutes its links back before storing anything.
///     </para>
///     <para>
///         <paramref name="TranslatedBy" /> is provenance — which models produced this, by which
///         path — stored on every rendering so "why do these two hundred read differently?" has
///         an answer.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TextTranslatedEvent(
    string SourceKey,
    /// <summary>
    ///     The marked text these renderings were produced from, verbatim. The consumer compares
    ///     it against what the source says <i>now</i> — a mismatch means an edit landed while the
    ///     batch was in flight, and these renderings describe words that no longer exist.
    /// </summary>
    string SourceText,
    string SourceLanguage,
    IReadOnlyDictionary<string, string> Translations,
    string TranslatedBy);
