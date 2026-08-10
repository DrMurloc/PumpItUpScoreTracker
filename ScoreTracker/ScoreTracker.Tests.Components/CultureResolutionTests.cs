using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using ScoreTracker.Domain.Records;
using ScoreTracker.Web.Services.Localization;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The ranking is positional, so nothing about it is self-evident from reading a provider:
///     an <c>Add</c> where an <c>Insert</c> belongs demotes the account setting below the cookie
///     and quietly restores the bug this all exists to fix.
/// </summary>
public sealed class CultureResolutionTests
{
    [Fact]
    public void RanksTheSavedSettingAboveTheCookieAndBelowAnExplicitQueryString()
    {
        var providers = CultureResolution.BuildOptions().RequestCultureProviders;

        Assert.Collection(providers,
            p => Assert.IsType<QueryStringRequestCultureProvider>(p),
            p => Assert.IsType<UserSettingRequestCultureProvider>(p),
            p => Assert.IsType<CookieRequestCultureProvider>(p),
            p => Assert.IsType<AcceptLanguageHeaderRequestCultureProvider>(p),
            p => Assert.IsType<CustomRequestCultureProvider>(p));
    }

    [Fact]
    public void OffersEveryShippedCatalogueAndFallsBackToEnglish()
    {
        var options = CultureResolution.BuildOptions();

        Assert.Equal(SupportedCultures.Codes().OrderBy(c => c),
            options.SupportedUICultures!.Select(c => c.Name).OrderBy(c => c));
        Assert.Equal(SupportedCultures.Default, options.DefaultRequestCulture.UICulture.Name);
    }

    /// <summary>
    ///     The last provider is the downward mapping for browsers sending a region we ship no
    ///     catalogue for — Chile, Peru and Argentina could never match before it existed.
    /// </summary>
    [Fact]
    public async Task PlacesABrowserRegionWeShipNoCatalogueFor()
    {
        var fallback = CultureResolution.BuildOptions().RequestCultureProviders.Last();
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "es-CL,es;q=0.9";

        var result = await fallback.DetermineProviderCultureResult(context);

        Assert.Equal("es-ES", Assert.Single(result!.UICultures).Value);
    }

    [Fact]
    public async Task DeclinesWhenTheBrowserAsksForNothingWeCanPlace()
    {
        var fallback = CultureResolution.BuildOptions().RequestCultureProviders.Last();
        var context = new DefaultHttpContext();
        context.Request.Headers.AcceptLanguage = "zz-ZZ";

        Assert.Null(await fallback.DetermineProviderCultureResult(context));
    }
}
