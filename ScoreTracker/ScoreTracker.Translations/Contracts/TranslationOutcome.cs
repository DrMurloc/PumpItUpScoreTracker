using ScoreTracker.Domain.Records;

namespace ScoreTracker.Translations.Contracts;

/// <summary>
///     Everything one comment produced: the English pivot, a rendering per target locale, and the
///     usage of every call it took.
///     <para>
///         <paramref name="Translations" /> is deliberately not keyed for every locale. The one
///         that speaks the comment's own language is absent, because rendering it would replace
///         the author's words with a paraphrase. <b>Absence is the instruction to display the
///         original</b>: render locale L from <c>Translations[L]</c> when it is there, and from
///         the comment itself when it is not. <see cref="TranslationTarget.ForSource" /> says
///         which locales to expect.
///     </para>
///     <para>
///         <paramref name="Calls" /> is on the contract rather than hidden in the handler because
///         a caller that cannot see what a translation consumed cannot hold it to a budget — which
///         the first consumer, a cost probe, exists to measure, and a nightly job would need to
///         enforce.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TranslationOutcome(
    PivotResult Pivot,
    IReadOnlyDictionary<string, string> Translations,
    IReadOnlyList<TranslationCall> Calls);

/// <summary>One model call inside a translation, named by the stage that made it.</summary>
[ExcludeFromCodeCoverage]
public sealed record TranslationCall(string Stage, string ModelId, LanguageModelUsage Usage);
