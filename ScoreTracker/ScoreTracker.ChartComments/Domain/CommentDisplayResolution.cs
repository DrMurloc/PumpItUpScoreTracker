using ScoreTracker.Translations.Contracts;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Which rendering a reader sees, owner-worded (2026-08-24) and judged by language, never
///     region:
///     <list type="number">
///         <item>Comment in the reader's own language → the original. Outranks everything,
///         including a stored manual pick — nobody is shown a translation of a comment written in
///         their own language.</item>
///         <item>Otherwise the reader's stored pick where a rendering exists for it, else the
///         rendering matching the reader's language.</item>
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
        // A caller that has not adopted translation display, or a reader the site cannot place,
        // reads originals — which is also what every reader saw before this feature existed.
        if (string.IsNullOrWhiteSpace(readerLocale)) return new Resolution(null, false);

        if (sourceLanguage != null && TranslationTarget.SharesLanguage(readerLocale, sourceLanguage))
            return new Resolution(null, false);

        if (preferredLocale != null)
        {
            var picked = availableLocales.FirstOrDefault(locale =>
                string.Equals(locale, preferredLocale, StringComparison.OrdinalIgnoreCase));
            if (picked != null) return new Resolution(picked, false);
        }

        var mapped = availableLocales.FirstOrDefault(locale =>
            TranslationTarget.SharesLanguage(locale, readerLocale));
        if (mapped != null) return new Resolution(mapped, false);

        // The queued badge belongs only to a reader whose default would be a rendering that is
        // still on its way — everyone else's default is the original and needs no explanation.
        var target = preferredLocale ?? readerLocale;
        var oneIsComing = queued && availableLocales.Count == 0 && RendersFor(target);

        return new Resolution(null, oneIsComing);
    }

    private static bool RendersFor(string locale)
    {
        return TranslationTarget.SharesLanguage(TranslationTarget.Pivot, locale)
               || TranslationTarget.All.Any(target => TranslationTarget.SharesLanguage(target, locale));
    }
}
