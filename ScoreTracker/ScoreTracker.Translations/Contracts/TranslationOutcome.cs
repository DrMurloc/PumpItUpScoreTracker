using ScoreTracker.Domain.Records;

namespace ScoreTracker.Translations.Contracts;

/// <summary>
///     Everything one comment produced: the English pivot, a rendering per target locale, and the
///     usage of every call it took.
///     <para>
///         <paramref name="Calls" /> is on the contract rather than hidden in the handler because
///         the first consumer is a cost probe — a caller that cannot see what a translation
///         consumed cannot answer the question the workbench exists to ask.
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
