using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public class CultureController : Controller
{
    private static readonly ISet<string> SupportedCultures =
        new[] { "en-US", "pt-BR", "ko-KR", "en-ZW" }.ToHashSet(StringComparer.OrdinalIgnoreCase);

    [HttpGet("Set")]
    public IActionResult Set([FromQuery(Name = "culture")] string culture,
        [FromQuery(Name = "redirectUrl")] string redirectUri)
    {
        if (SupportedCultures.Contains(culture))
            HttpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(
                    new RequestCulture(culture, culture)));

        return LocalRedirect(redirectUri);
    }
}