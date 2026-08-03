using Microsoft.AspNetCore.Mvc;

namespace ScoreTracker.Web.Controllers
{
    /// <summary>
    ///     Routes the console redraw retired. The activity log and the debug tools were two pages a
    ///     maker had to know existed; a maker looking at a failed delivery and a maker deciding
    ///     whether to replay it are the same person at the same moment, so both fold into the
    ///     Webhooks section.
    ///     Real MVC 301s: a redirect from a component is a 302, which does not consolidate.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class ToolConsoleRedirectController : Controller
    {
        [HttpGet("/Developers/{toolId:guid}/Console")]
        public IActionResult ConsoleToWebhooks(Guid toolId)
        {
            return RedirectPermanent($"/Developers/{toolId}/insights");
        }

        [HttpGet("/Developers/{toolId:guid}/Debug")]
        public IActionResult DebugToWebhooks(Guid toolId)
        {
            return RedirectPermanent($"/Developers/{toolId}/webhooks");
        }
    }
}
