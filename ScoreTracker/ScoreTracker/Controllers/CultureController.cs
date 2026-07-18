using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public class CultureController : Controller
{
    [HttpGet("Set")]
    public IActionResult Set([FromQuery(Name = "culture")] string culture,
        [FromQuery(Name = "redirectUrl")] string redirectUri)
    {
        if (SupportedCultures.IsSupported(culture))
            HttpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(
                    new RequestCulture(culture, culture)));

        return LocalRedirect(redirectUri);
    }
}