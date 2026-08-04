namespace ScoreTracker.Translations.Contracts;

/// <summary>
///     The locales a piece of community text is rendered into, and the English pivot every
///     rendering is produced from.
///     <para>
///         en-US is deliberately not in <see cref="All" />: it is the pivot, produced by the
///         first call, so asking the second call to emit it again would pay for a round trip
///         through a translation we already have.
///     </para>
///     <para>
///         es-MX is absent for now. Peninsular and Mexican Spanish are mutually intelligible, the
///         es-MX catalogue has a known contamination problem, and the original comment is always
///         displayed alongside the rendering. Because the English pivot is kept, adding es-MX
///         later is a second-call backfill rather than a re-translation.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public static class TranslationTarget
{
    /// <summary>The language every rendering is produced from — never itself a rendering.</summary>
    public const string Pivot = "en-US";

    public static readonly IReadOnlyList<string> All = new[] { "es-ES", "fr-FR", "ko-KR", "pt-BR" };
}
