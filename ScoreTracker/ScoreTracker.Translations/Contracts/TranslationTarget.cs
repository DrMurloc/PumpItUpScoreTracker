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

    /// <summary>
    ///     The locales worth rendering for a comment written in <paramref name="sourceLanguage" />
    ///     — every target except the one that speaks the language it arrived in.
    ///     <para>
    ///         Translating Korean into Korean is not a no-op, it is a rewrite, and a measurably
    ///         destructive one: across a real corpus the round trip raised casual endings into
    ///         polite ones, swapped the community's own vocabulary for neutral words, dropped the
    ///         most contemptuous phrase in a comment, turned Mexican <em>carnal</em> into
    ///         peninsular <em>tío</em>, escalated a mild compliment into profanity, and in one case
    ///         turned 1000% into 100%. The original is already perfect and already displayed;
    ///         paying to replace it with a paraphrase is worse than doing nothing.
    ///     </para>
    ///     <para>
    ///         Region is deliberately ignored, so a Mexican comment suppresses es-ES too. The
    ///         reasoning is the same one that kept es-MX off the target list: the two are mutually
    ///         intelligible, so a Spaniard reading Mexican Spanish loses nothing, while converting
    ///         it costs the author their voice.
    ///     </para>
    ///     <para>
    ///         Absence is the signal for the caller: render locale L from
    ///         <c>Translations[L]</c> when it is there, and from the original comment when it is
    ///         not.
    ///     </para>
    /// </summary>
    public static IReadOnlyList<string> ForSource(string? sourceLanguage)
    {
        if (string.IsNullOrWhiteSpace(sourceLanguage)) return All;

        return All.Where(locale => !SharesLanguage(locale, sourceLanguage)).ToArray();
    }

    private static bool SharesLanguage(string locale, string sourceLanguage)
    {
        return string.Equals(Primary(locale), Primary(sourceLanguage), StringComparison.OrdinalIgnoreCase);
    }

    private static string Primary(string tag)
    {
        var separator = tag.IndexOf('-');

        return separator < 0 ? tag.Trim() : tag[..separator].Trim();
    }
}
