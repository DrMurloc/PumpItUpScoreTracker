using System.Net;
using System.Xml.Linq;
using ScoreTracker.Tests.E2E.Support;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The endpoints that are not Razor components, driven through the real host.
///     <para>
///         Blazor's routes used to be a fallback — the lowest priority there is — so nothing else
///         could be shadowed by them. Render modes register component routes as real endpoints,
///         which makes the routing table something a change can genuinely break. The failure would
///         be silent: api/* would answer with the app's HTML instead of JSON, every suite would
///         stay green (Tests.Api mocks the mediator and never touches the pipeline), and the first
///         report would come from a community tool author whose bot stopped parsing.
///     </para>
/// </summary>
[Collection("E2E")]
public sealed class NonComponentEndpointTests : IAsyncLifetime
{
    private readonly E2EAppFixture _fixture;
    private HttpClient _client = null!;
    private Guid _chartId;

    public NonComponentEndpointTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _chartId = await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 20, "Single");
        _client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     The public API is a contract community tools build against, and it is token-gated —
    ///     so an anonymous caller is turned away by the controller. Being turned away is the
    ///     point: a route shadowed by the app would answer 200 with HTML, and every bot parsing
    ///     it would break while every suite stayed green.
    /// </summary>
    [Fact]
    public async Task TheChartsApiIsStillTheApiAndNotTheApp()
    {
        var response = await _client.GetAsync("/api/charts?mix=Phoenix");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("shell-appbar", body);
    }

    /// <summary>Swagger is how integrators discover that contract.</summary>
    [Fact]
    public async Task SwaggerStillServes()
    {
        var response = await _client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    ///     Google rejects a sitemap whose elements sit outside the sitemap namespace.
    ///     LINQ-to-XML children do not inherit their parent's namespace, so the regression
    ///     serializes every url element with an empty xmlns and the whole file reads as
    ///     invalid — this parses the document and holds each element to the namespace.
    /// </summary>
    [Fact]
    public async Task TheSitemapIsNamespaceValidXml()
    {
        var response = await _client.GetAsync("/sitemap.xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/xml", response.Content.Headers.ContentType?.ToString());

        var body = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<?xml", body);

        var document = XDocument.Parse(body);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        Assert.Equal(ns + "urlset", document.Root!.Name);
        Assert.All(document.Descendants(), element => Assert.Equal(ns, element.Name.Namespace));

        var urls = document.Descendants(ns + "loc").Select(loc => loc.Value).ToArray();
        Assert.Contains("https://piuscores.arroweclip.se/Welcome", urls);
        // Canonical vanity URLs, never GUIDs — the seeded Conflict S20 sits at its slug path.
        Assert.Contains("https://piuscores.arroweclip.se/Charts/phoenix/conflict/s20", urls);
    }

    /// <summary>
    ///     Unmatched routes fall to the catch-all page, whose NotFound() renders the branded
    ///     not-found page in the same response: a true HTTP 404 for crawlers, the MISS screen
    ///     inside the shell for a human.
    /// </summary>
    [Fact]
    public async Task UnknownRoutesAnswer404WithTheMissPage()
    {
        var response = await _client.GetAsync("/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("nf-miss", body);
        Assert.Contains("shell-appbar", body);
    }

    /// <summary>Crawlers discover the sitemap through robots.txt, not Search Console alone.</summary>
    [Fact]
    public async Task RobotsTxtPointsCrawlersAtTheSitemap()
    {
        var response = await _client.GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Sitemap: https://piuscores.arroweclip.se/sitemap.xml", body);
    }

    /// <summary>
    ///     The chart head is the SEO payoff: a crawler runs no circuit, so the chart's name,
    ///     description and jacket must be in the raw HTML the server returns — this reads the
    ///     document exactly as a crawler does, no browser.
    /// </summary>
    [Fact]
    public async Task TheChartPageServesItsHeadWithoutACircuit()
    {
        // One clean score makes the description verdict-flavored — the population stats
        // are what give every chart page its own snippet text.
        var user = await _fixture.Seed.SeedUserAsync("HeadFact");
        await _fixture.Seed.SeedPhoenixScoreAsync(user, _chartId, 985_000);

        var response = await _client.GetAsync($"/Chart/{_chartId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>Conflict S20 | PIU Scores</title>", body);
        Assert.Contains("name=\"description\"", body);
        Assert.Contains("1 score tracked, 100% pass rate.", body);
        Assert.Contains("property=\"og:image\"", body);
        Assert.Contains("property=\"og:site_name\"", body);
        Assert.Contains("name=\"twitter:card\"", body);
        // The appearance layer rides the same static head: the JSON-LD graph (song +
        // breadcrumb trail, shown in place of raw URL slugs) and the stat tiles'
        // data-nosnippet, which keeps label soup out of search snippets so the
        // description is what a result quotes.
        Assert.Contains("application/ld+json", body);
        Assert.Contains("BreadcrumbList", body);
        Assert.Contains("data-nosnippet", body);
    }

    /// <summary>
    ///     The front door carries the site-name signals: WebSite JSON-LD plus og:site_name
    ///     on the root is what lets a search result say "PIU Scores" instead of the bare
    ///     domain. Its title carries the searchable descriptor, and data-nosnippet keeps
    ///     the sign-in buttons and mocked-up card numbers out of search snippets.
    /// </summary>
    [Fact]
    public async Task TheFrontDoorNamesTheSiteForSearchEngines()
    {
        var response = await _client.GetAsync("/Welcome");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Two-part pin: the em-dash separator serves as a numeric entity (the HTML encoder
        // escapes non-ASCII), so the assert brackets it rather than spelling it.
        Assert.Contains("<title>PIU Scores ", body);
        Assert.Contains("Pump It Up score tracker &amp; tier lists</title>", body);
        Assert.Contains("\"@type\":\"WebSite\"", body);
        Assert.Contains("\"name\":\"PIU Scores\"", body);
        Assert.Contains("property=\"og:site_name\"", body);
        Assert.Contains("data-nosnippet", body);
    }

    /// <summary>
    ///     A chart with siblings renders their DifficultyBubbles statically — and those wrap a
    ///     MudTooltip, which must survive static SSR. The lone-chart head fact above never
    ///     exercises this path (one difficulty, no sibling row), so a chart that has siblings
    ///     is what proves the hero's static section doesn't throw on a popover component.
    /// </summary>
    [Fact]
    public async Task AChartWithSiblingsRendersStaticallyWithoutThrowing()
    {
        await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 24, "Double");

        var response = await _client.GetAsync($"/Chart/{_chartId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Both siblings' bubbles are in the raw HTML — the static hero rendered its
        // MudTooltip-wrapped bubbles, pre-circuit.
        Assert.Contains("chart-hero-siblings", body);
    }

    /// <summary>
    ///     Routes the head resolver doesn't know keep the bare site title and gain no meta —
    ///     a shared description on every URL would read as sitewide duplicate content.
    /// </summary>
    [Fact]
    public async Task UnmatchedRoutesFallBackToTheSiteTitle()
    {
        var response = await _client.GetAsync("/TierLists");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>PIU Scores</title>", body);
        Assert.DoesNotContain("property=\"og:image\"", body);
    }

    /// <summary>
    ///     The Hangfire dashboard is admin-only. Anonymous must be turned away — not handed the
    ///     app, which is what a shadowed route would do.
    /// </summary>
    [Fact]
    public async Task TheHangfireDashboardDoesNotServeTheAppToAnonymous()
    {
        var response = await _client.GetAsync("/hangfire");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("shell-appbar", body);
    }
}
