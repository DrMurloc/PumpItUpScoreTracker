using System.Globalization;
using Microsoft.Extensions.Localization;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Accessors;

/// <summary>
///     Bridges the app's resx catalogues (Web-owned, keyed by English text) to non-HTTP
///     composers — Discord cards and replies — via <see cref="ILocalizedTextAccessor" />.
///     <see cref="IStringLocalizer" /> reads the ambient UI culture, so each lookup swaps
///     both current cultures to the target for its duration: the UI culture selects the
///     catalogue, the formatting culture makes numeric/date arguments render per-locale.
///     Null, unknown, or unsupported cultures resolve to the English default, and a
///     missing key falls back to the key itself (the English text).
/// </summary>
public sealed class ResxLocalizedTextAccessor : ILocalizedTextAccessor
{
    private readonly IStringLocalizer _localizer;

    public ResxLocalizedTextAccessor(IStringLocalizerFactory factory)
    {
        _localizer = factory.Create(typeof(App));
    }

    public string Get(string? culture, string key)
    {
        return WithCulture(culture, () => _localizer[key].Value);
    }

    public string Get(string? culture, string key, params object[] args)
    {
        return WithCulture(culture, () => _localizer[key, args].Value);
    }

    private static string WithCulture(string? culture, Func<string> read)
    {
        var target = CultureInfo.GetCultureInfo(SupportedCultures.Normalize(culture));
        var originalUi = CultureInfo.CurrentUICulture;
        var originalFormat = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentUICulture = target;
            CultureInfo.CurrentCulture = target;
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
            CultureInfo.CurrentCulture = originalFormat;
        }
    }
}
