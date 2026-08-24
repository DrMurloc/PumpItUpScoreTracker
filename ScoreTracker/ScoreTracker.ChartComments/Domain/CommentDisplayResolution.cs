using ScoreTracker.Translations.Contracts;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Which rendering a reader sees, owner-worded (2026-08-24) and judged by language, never
///     region. <b>A manual pick is total</b> (owner, field test, same day): "Read in Spanish"
///     substitutes for the reader's own locale entirely, so the one resolution runs for whoever
///     the reader asked to read as:
///     <list type="number">
///         <item>Comment written in the effective language → the original. A Spanish comment
///         under a Spanish pick IS the Spanish the reader asked for — never a translation of a
///         comment into its own language.</item>
///         <item>Otherwise the rendering matching the effective language.</item>
///         <item>No match → the original. Not forced English.</item>
///     </list>
///     Pure, so the whole table of cases is unit-testable without a repository in sight.
/// </summary>
internal static class CommentDisplayResolution
{
    /// <summary>Null <paramref name="RenderingLocale" /> means show the original.</summary>
    internal sealed record Resolution(string? RenderingLocale, bool Pending);

    public static Resolution Resolve(string? readerLocale, string? preferredLocale, string? sourceLanguage,
        IReadOnlyList<string> availableLocales, bool queued)
    {
        // The pick replaces the reader, wholesale. Half-honouring it — own-language comments
        // slipping back to the reader's locale — is how "Read in español" once showed a Spanish
        // comment in English: the pick had no rendering there, and the fallback mapped to the
        // reader instead of to what they asked for.
        var effective = string.IsNullOrWhiteSpace(preferredLocale) ? readerLocale : preferredLocale;

        // A caller that has not adopted translation display, or a reader the site cannot place,
        // reads originals — which is also what every reader saw before this feature existed.
        if (string.IsNullOrWhiteSpace(effective)) return new Resolution(null, false);

        if (sourceLanguage != null && TranslationTarget.SharesLanguage(effective, sourceLanguage))
            return new Resolution(null, false);

        var match = availableLocales.FirstOrDefault(locale =>
            TranslationTarget.SharesLanguage(locale, effective));
        if (match != null) return new Resolution(match, false);

        // The queued badge belongs only to a reader whose view would be a rendering that is
        // still on its way — everyone else sees the original and needs no explanation.
        var oneIsComing = queued && availableLocales.Count == 0 && RendersFor(effective);

        return new Resolution(null, oneIsComing);
    }

    private static bool RendersFor(string locale)
    {
        return TranslationTarget.SharesLanguage(TranslationTarget.Pivot, locale)
               || TranslationTarget.All.Any(target => TranslationTarget.SharesLanguage(target, locale));
    }
}
