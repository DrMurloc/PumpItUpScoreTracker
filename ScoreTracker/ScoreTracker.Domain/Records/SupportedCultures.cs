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

    public static string[] Codes()
    {
        return All.Select(c => c.Code).ToArray();
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
