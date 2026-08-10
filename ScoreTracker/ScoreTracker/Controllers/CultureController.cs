using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.Records;
using ScoreTracker.Web.Services.Localization;

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
            CultureCookie.Write(HttpContext.Response, CultureCookie.ValueFor(culture));

        return LocalRedirect(redirectUri);
    }
}