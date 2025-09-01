using System.Net.Mime;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.Enums;
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
            var pages = (from chart in await _charts.GetCharts(MixEnum.Phoenix)
                select $"https://piuscores.arroweclip.se/Chart/{chart.Id}").ToList();
            pages.Add("https://piuscores.arroweclip.se/TierLists");
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
