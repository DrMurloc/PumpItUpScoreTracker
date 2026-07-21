using System.Text;
using System.Xml.Linq;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services;

namespace ScoreTracker.Web.Controllers
{
    [Route("")]
    public sealed class SitemapController : Controller
    {
        // Children never inherit an XML namespace: every element must carry it explicitly,
        // or it serializes as <url xmlns=""> and validators reject the whole file.
        private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        private readonly IMediator _mediator;

        public SitemapController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [Route("sitemap.xml")]
        public async Task<IActionResult> GetSitemap(CancellationToken cancellationToken)
        {
            var charts = (await _mediator.Send(new GetChartsQuery(ChartUrlResolver.DefaultMix), cancellationToken))
                .ToArray();
            // The front door: anonymous "/" and "/Login" both resolve to the same page,
            // which canonicalizes itself to /Welcome — only the canonical is listed.
            var pages = new List<string> { "https://piuscores.arroweclip.se/Welcome" };
            // Canonical vanity URLs, never GUIDs: the pretty URL is the one to index, and it
            // is what the GUID permalink 301s to (ChartPermalinkController).
            pages.AddRange(charts.Select(chart => $"https://piuscores.arroweclip.se{chart.CanonicalPath()}"));
            // Charts the current mix dropped are canonical in the mix they debuted in
            // (owner, 2026-07-20), so the whole back catalogue is crawlable — every legacy
            // chart is listed exactly once, at its debut appearance.
            var current = charts.Select(c => c.Id).ToHashSet();
            foreach (var mix in Enum.GetValues<MixEnum>().Where(m => m != ChartUrlResolver.DefaultMix))
                pages.AddRange((await _mediator.Send(new GetChartsQuery(mix), cancellationToken))
                    .Where(c => c.OriginalMix == mix && !current.Contains(c.Id))
                    .Select(chart => $"https://piuscores.arroweclip.se{chart.CanonicalPath()}"));
            pages.Add("https://piuscores.arroweclip.se/TierLists");
            // The challenges hub — a fresh weekly chart set + a daily chart, now real HTML
            // (weekly-charts-overhaul.md §3.4). Absent before the static rebuild.
            pages.Add("https://piuscores.arroweclip.se/WeeklyCharts");
            // Tier-lists overhaul C3: one canonical URL per Singles/Doubles folder that
            // actually has charts — each is an indexable community tier list.
            pages.AddRange(charts
                .Where(c => c.Type is ChartType.Single or ChartType.Double)
                .Select(c => (c.Type, Level: (int)c.Level))
                .Distinct()
                .OrderBy(f => f.Type).ThenBy(f => f.Level)
                .Select(f => $"https://piuscores.arroweclip.se/TierLists/{f.Type}/{f.Level}"));
            pages.Add("https://piuscores.arroweclip.se/ChartRandomizer");
            pages.Add("https://piuscores.arroweclip.se/PhoenixCalculator");
            pages.Add("https://piuscores.arroweclip.se/LifeCalculator");
            pages.Add("https://piuscores.arroweclip.se/PhoenixToXXCalculator");

            var urlset = new XElement(Ns + "urlset",
                pages.Select(page => new XElement(Ns + "url", new XElement(Ns + "loc", page))));
            var document = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);

            // ToString() drops the XML declaration; Save through a writer is what emits it.
            var xml = new StringBuilder();
            using (var writer = new Utf8StringWriter(xml))
            {
                document.Save(writer);
            }

            return Content(xml.ToString(), "application/xml; charset=utf-8");
        }

        /// <summary>
        ///     StringWriter reports UTF-16, and XDocument.Save writes the writer's encoding
        ///     into the declaration — this pins it to the UTF-8 the response actually ships.
        /// </summary>
        private sealed class Utf8StringWriter : StringWriter
        {
            public Utf8StringWriter(StringBuilder builder) : base(builder)
            {
            }

            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}
