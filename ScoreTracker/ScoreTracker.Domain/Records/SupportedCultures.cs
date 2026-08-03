namespace ScoreTracker.Domain.Records;

/// <summary>One supported locale: the culture code and the language's own name for itself.</summary>
[ExcludeFromCodeCoverage]
public sealed record SupportedCulture(string Code, string NativeName);

/// <summary>
///     The application's supported locales — the single source consumed by the
///     request-localization setup, the culture endpoint's validation, the account
///     language picker, and the Discord bot's per-channel/per-user message cultures.
///     Codes match the <c>Resources/App.&lt;code&gt;.resx</c> catalogues; en-US is the
///     key language and the fallback everywhere.
/// </summary>
[ExcludeFromCodeCoverage]
public static class SupportedCultures
{
    public const string Default = "en-US";

    public static readonly IReadOnlyList<SupportedCulture> All = new SupportedCulture[]
    {
        new("en-US", "English"),
        new("es-MX", "Español (México)"),
        new("es-ES", "Español (España)"),
        new("pt-BR", "Português"),
        new("ko-KR", "한국어"),
        new("ja-JP", "日本語"),
        new("fr-FR", "Français"),
        new("it-IT", "Italiano"),
        new("en-ZW", "Murloc")
    };

    /// <summary>
    ///     The locale a bare language subtag translates into. Every catalogue we ship is a
    ///     <em>specific</em> culture (es-MX, ja-JP, …) and ASP.NET's request localization only
    ///     falls back <em>upward</em> — es-CL resolves to es, which is not a catalogue, so an
    ///     anonymous visitor sending anything but one of our exact nine tags lands on English.
    ///     This table is the downward half. Murloc is deliberately absent: en-ZW is reachable
    ///     only by asking for it exactly, never as a fallback for "en".
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> PrimaryLanguageDefaults =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "en-US",
            // es → Spain, not Mexico (owner, 2026-08-03).
            ["es"] = "es-ES",
            ["pt"] = "pt-BR",
            ["ko"] = "ko-KR",
            ["ja"] = "ja-JP",
            ["fr"] = "fr-FR",
            ["it"] = "it-IT"
        };

    public static string[] Codes()
    {
        return All.Select(c => c.Code).ToArray();
    }

    /// <summary>
    ///     The closest supported locale for one browser language tag, or null when there is no
    ///     sensible match. An exactly-supported tag wins and is never re-regioned (es-MX stays
    ///     es-MX); otherwise the primary subtag picks the catalogue. Pure string work — no
    ///     <see cref="System.Globalization.CultureInfo" /> is constructed, so a malformed,
    ///     unknown, or wildcard tag returns null rather than throwing.
    /// </summary>
    public static string? ResolveClosest(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag)) return null;

        var tag = languageTag.Trim();

        var exact = NormalizeOrNull(tag);
        if (exact != null) return exact;

        var separator = tag.IndexOf('-');
        var primary = separator < 0 ? tag : tag[..separator];

        return PrimaryLanguageDefaults.TryGetValue(primary, out var code) ? code : null;
    }

    public static bool IsSupported(string? code)
    {
        return code != null && All.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The stored form of a culture: a supported code normalized to its canonical casing, else the default.</summary>
    public static string Normalize(string? code)
    {
        return All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))?.Code
               ?? Default;
    }

    /// <summary>
    ///     The optional stored form: canonical casing for a supported code, null (meaning
    ///     "English default") for anything absent or unsupported.
    /// </summary>
    public static string? NormalizeOrNull(string? code)
    {
        return All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    /// <summary>The language's own name for itself, for a stored code (null/unknown → English).</summary>
    public static string NativeNameFor(string? code)
    {
        return All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
            ?.NativeName ?? All[0].NativeName;
    }
}
