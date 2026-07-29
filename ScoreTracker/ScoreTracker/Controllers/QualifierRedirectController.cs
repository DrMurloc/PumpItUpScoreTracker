using Microsoft.AspNetCore.Mvc;

namespace ScoreTracker.Web.Controllers
{
    /// <summary>
    ///     Routes the qualifiers overhaul retired. The submit page folded into the one player
    ///     page as a dialog, and the tournament "Admin" link finally has somewhere to go — it
    ///     was rendered for head organisers for years while no page declared that route.
    ///     Real MVC 301s: a redirect from a component is a 302, which does not consolidate.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class QualifierRedirectController : Controller
    {
        [HttpGet("/Tournament/{tournamentId:guid}/Qualifiers/Submit")]
        public IActionResult SubmitToBoard(Guid tournamentId)
        {
            return RedirectPermanent($"/Tournament/{tournamentId}/Qualifiers");
        }

        [HttpGet("/Tournament/{tournamentId:guid}/Admin")]
        public IActionResult AdminToQualifiersAdmin(Guid tournamentId)
        {
            return RedirectPermanent($"/Tournament/{tournamentId}/Qualifiers/Admin");
        }
    }
}
