using System.Net.Mime;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Controllers
{
    [Route("")]
    public sealed class SitemapController : Controller
    {
        [ApiExplorerSettings(IgnoreApi = true)]
        [Route("sitemap.xml")]
        public async Task<IActionResult> GetSitemap([FromServices] IChartRepository _charts)
        {
            var charts = (await _charts.GetCharts(MixEnum.Phoenix)).ToArray();
            var pages = charts.Select(chart => $"https://piuscores.arroweclip.se/Chart/{chart.Id}").ToList();
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


            var ns = "{http://www.sitemaps.org/schemas/sitemap/0.9}";
            var urlset = new XElement(ns + "urlset");

            foreach (var t in pages)
                urlset.Add(new XElement("url",
                    new XElement("loc", t)
                ));
            return Content(urlset.ToString(), MediaTypeNames.Application.Xml);
        }
    }
}
