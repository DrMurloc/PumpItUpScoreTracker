using Microsoft.AspNetCore.Localization;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Web.Services.Localization;

/// <summary>
///     The resolution order, built in one place so it can be asserted rather than trusted
///     (docs/design/culture-resolution.md §1). Position in this list <em>is</em> the ranking:
///     query string, then the signed-in player's saved setting, then the cookie, then the
///     browser. An <c>Add</c> where an <c>Insert</c> belongs silently demotes the account
///     setting below the cookie and reinstates the bug, with every suite still green — which is
///     why <c>CultureResolutionTests</c> pins the sequence.
/// </summary>
public static class CultureResolution
{
    public static RequestLocalizationOptions BuildOptions()
    {
        var localization = new RequestLocalizationOptions()
            .AddSupportedCultures(SupportedCultures.Codes())
            .AddSupportedUICultures(SupportedCultures.Codes())
            .SetDefaultCulture(SupportedCultures.Default);

        // Rank 2, above the cookie: a signed-in player's saved language is the answer. Only an
        // explicit ?culture= (index 0, a deliberately one-request preview) may outrank it.
        localization.RequestCultureProviders.Insert(1, new UserSettingRequestCultureProvider());

        // Appended AFTER the three stock providers, so it only speaks when they found nothing: an
        // explicit ?culture= or the saved cookie still wins, and an exactly-supported
        // Accept-Language tag is still matched by the stock header provider. What reaches here is
        // the case that used to fall through to English — a bare "es"/"ja", or a region we carry
        // no catalogue for (es-CL, pt-PT, fr-CA). ResolveClosest maps those down; anything it
        // can't place returns null, which leaves the default culture exactly as before.
        localization.RequestCultureProviders.Add(new CustomRequestCultureProvider(context =>
        {
            foreach (var language in context.Request.GetTypedHeaders().AcceptLanguage
                         .OrderByDescending(l => l.Quality ?? 1d))
            {
                var resolved = SupportedCultures.ResolveClosest(language.Value.Value);
                if (resolved != null)
                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(resolved));
            }

            return Task.FromResult<ProviderCultureResult?>(null);
        }));

        return localization;
    }
}
