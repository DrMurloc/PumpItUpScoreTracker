using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.Records;
using ScoreTracker.Web.Services.Localization;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public class CultureController : Controller
{
    [HttpGet("Set")]
    public IActionResult Set([FromQuery(Name = "culture")] string? culture,
        [FromQuery(Name = "redirectUrl")] string? redirectUri)
    {
        if (SupportedCultures.IsSupported(culture))
            CultureCookie.Write(HttpContext.Response, CultureCookie.ValueFor(culture!));

        // A missing or off-site target lands on the home page rather than throwing: LocalRedirect
        // rejects both, and this is the one endpoint a language change navigates through, so a
        // truncated or hand-typed link would otherwise be an error page instead of a language.
        return LocalRedirect(Url.IsLocalUrl(redirectUri) ? redirectUri! : "/");
    }
}