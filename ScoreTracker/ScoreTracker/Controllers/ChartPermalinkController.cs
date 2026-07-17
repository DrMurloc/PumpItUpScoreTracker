using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Web.Services;

namespace ScoreTracker.Web.Controllers
{
    /// <summary>
    ///     The GUID permalink and its legacy aliases. These are the stable identity of a
    ///     chart — they 301 to the canonical vanity URL forever (docs/design/chart-details-
    ///     overhaul.md), so every old link, bookmark and Discord unfurl keeps resolving while
    ///     the signals consolidate onto the pretty URL. In-app links build the canonical
    ///     directly (chart.CanonicalPath()); this controller serves external and legacy ones.
    ///     A real MVC 301 — a redirect from a static component is a 302, which doesn't
    ///     consolidate. Historical (mix, song, level) triples can't live here (they share the
    ///     canonical route's shape); the page issues those 301s itself.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class ChartPermalinkController : Controller
    {
        [HttpGet("/Chart/{id:guid}")]
        public async Task<IActionResult> ByGuid(Guid id, [FromServices] ChartUrlResolver resolver,
            CancellationToken cancellationToken)
        {
            var canonical = await resolver.CanonicalPathFor(id, ChartUrlResolver.DefaultMix, cancellationToken);
            return canonical == null ? NotFound() : RedirectPermanent(canonical);
        }

        // Bare /Chart and the legacy /Record alias had no identity of their own — they were
        // the chart finder, which the shell's search replaced. Send them to the catalog.
        [HttpGet("/Chart")]
        [HttpGet("/Record")]
        public IActionResult ToCatalog()
        {
            return RedirectPermanent("/Charts");
        }
    }
}
