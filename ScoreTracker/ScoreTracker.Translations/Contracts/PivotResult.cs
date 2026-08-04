namespace ScoreTracker.Translations.Contracts;

/// <summary>
///     A proper noun, identifier, or number that must survive translation. <paramref name="Surface" />
///     is how the author wrote it, <paramref name="Canonical" /> is the form the site knows it by —
///     a Korean comment writes 피펨즈 for the player the rest of the world calls Fefemz.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TranslationEntity(string Surface, string Canonical, string Kind);

/// <summary>
///     The English rendering of a comment, plus how it was written.
///     <para>
///         The metadata is the whole reason a pivot is viable. English cannot carry a Korean
///         speech level or a tú/usted choice, so translating through it would silently flatten
///         the author's register — the one thing we promised to preserve. Naming register as
///         explicit fields makes it a decision the model has to commit to instead of a nuance it
///         can drop.
///     </para>
///     <para>
///         <paramref name="FormalityMarked" /> is false when the source language never encoded a
///         formality level at all (English usually does not), which is the case where the
///         renderer applies a house default rather than mirroring something that was never there.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PivotResult(
    string SourceLanguage,
    string English,
    string Register,
    bool FormalityMarked,
    string Tone,
    IReadOnlyList<TranslationEntity> Entities);
