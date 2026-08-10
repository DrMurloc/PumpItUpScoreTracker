using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace ScoreTracker.Web.Services.Localization;

/// <summary>
///     The culture cookie's one spelling (docs/design/culture-resolution.md). It is a cache, and
///     the only place an anonymous visitor can keep a language choice — they have no row to write
///     to — so it has to outlive the browser session.
///     <para>
///         Nothing gives it a lifetime for free: <c>AddCookiePolicy</c> is registered but
///         <c>app.UseCookiePolicy()</c> is never called, so a bare <c>Response.Cookies.Append</c>
///         writes a <em>session</em> cookie. Every append goes through <see cref="Write" /> so
///         that can't be forgotten again.
///     </para>
/// </summary>
public static class CultureCookie
{
    /// <summary>
    ///     How long an anonymous visitor's choice survives. <c>MaxAge</c> is absolute and nothing
    ///     renews it — the write-back fires only when the resolved culture differs from the
    ///     cookie, which it never does once the cookie is the provider that won — so this is the
    ///     real deadline on a choice, not a sliding window like the auth cookie's. 400 days is
    ///     the ceiling browsers will honour for a cookie lifetime anyway.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(400);

    public static readonly string Name = CookieRequestCultureProvider.DefaultCookieName;

    public static string ValueFor(string culture)
    {
        return Value(new RequestCulture(culture, culture));
    }

    public static string ValueFor(CultureInfo culture, CultureInfo uiCulture)
    {
        return Value(new RequestCulture(culture, uiCulture));
    }

    public static void Write(HttpResponse response, string value)
    {
        response.Cookies.Append(Name, value, new CookieOptions
        {
            // MaxAge rather than Expires: browsers prefer it, and it needs no clock reading.
            MaxAge = Lifetime,
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        });
    }

    private static string Value(RequestCulture culture)
    {
        return CookieRequestCultureProvider.MakeCookieValue(culture);
    }
}
