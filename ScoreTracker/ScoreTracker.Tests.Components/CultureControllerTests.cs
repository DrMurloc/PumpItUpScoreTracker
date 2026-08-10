using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Web.Controllers;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The endpoint both language pickers navigate through. A circuit cannot touch cookies, so
///     everything the browser has to be told about a language change happens here.
/// </summary>
public sealed class CultureControllerTests
{
    private static CultureController Controller(out HttpContext context)
    {
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl(It.IsAny<string>()))
            .Returns((string? target) => target != null && target.StartsWith('/') && !target.StartsWith("//"));

        context = new DefaultHttpContext();
        return new CultureController
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            Url = url.Object
        };
    }

    private static string? CookieHeader(HttpContext context)
    {
        return context.Response.Headers.SetCookie.FirstOrDefault(h => h != null && h.StartsWith(".AspNetCore.Culture"));
    }

    [Fact]
    public void SetWritesTheChoiceAndReturnsToThePageItCameFrom()
    {
        var controller = Controller(out var context);

        var result = Assert.IsType<LocalRedirectResult>(controller.Set("es-ES", "/Account"));

        Assert.Equal("/Account", result.Url);
        Assert.Contains("es-ES", CookieHeader(context));
    }

    [Fact]
    public void SetIgnoresACultureWeDoNotShip()
    {
        var controller = Controller(out var context);

        controller.Set("zz-ZZ", "/Account");

        Assert.Null(CookieHeader(context));
    }

    /// <summary>Expiring the cookie is how the browser is told to decide for itself again.</summary>
    [Fact]
    public void ClearExpiresTheCookieSoTheBrowserDecidesAgain()
    {
        var controller = Controller(out var context);

        var result = Assert.IsType<LocalRedirectResult>(controller.Clear("/Account"));

        Assert.Equal("/Account", result.Url);
        Assert.Contains("expires=Thu, 01 Jan 1970", CookieHeader(context));
    }

    /// <summary>
    ///     Both actions take a redirect target straight off the query string, so neither may
    ///     throw on one that is missing or points somewhere else entirely.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/phish")]
    [InlineData("//example.com/phish")]
    public void ARedirectTargetThatIsNotOursLandsHome(string? redirectUrl)
    {
        var controller = Controller(out _);

        Assert.Equal("/", Assert.IsType<LocalRedirectResult>(controller.Set(SupportedCultures.Default, redirectUrl)).Url);
        Assert.Equal("/", Assert.IsType<LocalRedirectResult>(controller.Clear(redirectUrl)).Url);
    }

    /// <summary>The sentinel is a picker value, never a culture — Set must not write it.</summary>
    [Fact]
    public void SetRefusesTheAutomaticSentinel()
    {
        var controller = Controller(out var context);

        controller.Set(SupportedCultures.Automatic, "/Account");

        Assert.Null(CookieHeader(context));
    }
}
