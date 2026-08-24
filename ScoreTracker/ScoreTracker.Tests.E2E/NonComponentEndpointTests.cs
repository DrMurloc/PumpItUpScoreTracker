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
    ///     The shell's mix menu renders an anchor per mix on every page, each one
    ///     /Mix/Set?mix=X&amp;redirectUrl=&lt;this page&gt;. Left crawlable that is 31 redirect-only
    ///     URLs per indexable page — the whole chart catalogue multiplied — so the endpoint is
    ///     robots-blocked and the casing has to match the anchors, which robots.txt compares
    ///     case-sensitively.
    /// </summary>
    [Fact]
    public async Task RobotsTxtKeepsCrawlersOffTheMixSwitchEndpoint()
    {
        var response = await _client.GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Disallow: /Mix/Set", body);
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
    ///     The PUMBILITY calculator exists to be found and quoted (docs/design/pumbility-calculator.md
    ///     D2): the formula, both chart types' value tables, the constants block the script reads
    ///     and the head must all be in the raw HTML — the static renderer once dropped a script
    ///     element silently, and a browser would never show that regression. Read as a crawler reads.
    /// </summary>
    [Fact]
    public async Task ThePumbilityCalculatorServesItsFormulaTablesAndHeadWithoutACircuit()
    {
        var response = await _client.GetAsync("/PumbilityCalculator/phoenix-2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // The em dash in the title serves as a numeric entity, so the assert brackets it.
        Assert.Contains("<title>PUMBILITY Calculator ", body);
        Assert.Contains("Phoenix 2 | PIU Scores</title>", body);
        Assert.Contains("rel=\"canonical\" href=\"https://piuscores.arroweclip.se/PumbilityCalculator/phoenix-2\"", body);
        Assert.Contains("\"@type\":\"TechArticle\"", body);
        Assert.Contains("BreadcrumbList", body);
        // The formula and both types' tables are real markup, the second type hidden not absent.
        Assert.Contains("Base(level)", body);
        Assert.Contains("data-pc-type=\"Single\"", body);
        Assert.Contains("data-pc-type=\"Double\"", body);
        // A cell the configuration prices: D24 at S is 250 × 1.45.
        Assert.Contains("data-v=\"362.5\" data-l=\"24\" data-g=\"S\"", body);
        // The constants block the script multiplies survived the static renderer.
        Assert.Contains("data-pc-constants", body);
        Assert.Contains("\"additive\":true", body);
        Assert.Contains("\"singlesUp\":true", body);
        // And the script that works the page is included under its hashed name.
        Assert.Contains("pumbility-calculator", body);
    }

    [Theory]
    [InlineData("/PhoenixCalculator/phoenix-2", "Phoenix 2", "920,000")]
    [InlineData("/PhoenixCalculator/phoenix", "Phoenix", "900,000")]
    public async Task ThePhoenixScorePageServesItsFormulaLadderAndHeadWithoutACircuit(string path,
        string mixName, string aaFloor)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // The em dash in the title serves as a numeric entity, so the assert brackets it.
        Assert.Contains("<title>Phoenix Score Calculator ", body);
        Assert.Contains($"{mixName} | PIU Scores</title>", body);
        Assert.Contains($"rel=\"canonical\" href=\"https://piuscores.arroweclip.se{path}\"", body);
        Assert.Contains("\"@type\":\"TechArticle\"", body);
        Assert.Contains("BreadcrumbList", body);
        // The formula and the mix's own ladder are real markup — the AA floor differs per mix.
        Assert.Contains("1,000,000 ⌋", body);
        Assert.Contains(aaFloor, body);
        // The calculator's markup and the measured sections' frames are real HTML even on an
        // empty database — the data sections render their not-yet states rather than vanishing.
        Assert.Contains("data-sc-calc", body);
        Assert.Contains("How many notes is a level?", body);
        // The constants block the script computes from survived the static renderer (the K3
        // script-drop guard), carrying both mixes' floor tables.
        Assert.Contains("data-sc-constants", body);
        Assert.Contains("\"floor\":920000", body);
        Assert.Contains("\"floor\":925000", body);
        // And the script that works the page is included under its hashed name.
        Assert.Contains("phoenix-calculator", body);
    }

    [Fact]
    public async Task TheOldRatingCalculatorNameRedirectsPermanently()
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(_fixture.BaseUrl) };

        var response = await client.GetAsync("/RatingCalculator");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/PumbilityCalculator", response.Headers.Location?.ToString());
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
    ///     A region we ship no catalogue for still gets its language. Chile, Peru and Argentina
    ///     are a large share of the playerbase and none of their tags match es-MX or es-ES on
    ///     their own — request localization only falls back upward, so before the downward
    ///     mapping these visitors read an English front door.
    /// </summary>
    [Theory]
    [InlineData("es-CL")]
    [InlineData("es-PE,es;q=0.9,en;q=0.8")]
    [InlineData("es")]
    public async Task TheFrontDoorSpeaksSpanishToSpanishBrowsersWeHaveNoCatalogueFor(string acceptLanguage)
    {
        var body = await GetFrontDoorWithLanguage(acceptLanguage);

        // The encoder escapes non-ASCII, so the assert stops before the accent in "sesión".
        Assert.Contains("Iniciar sesi", body);
    }

    /// <summary>
    ///     The owner's requirement, stated plainly: no error page on a culture nobody tested a
    ///     browser in. These headers are unplaceable, wildcard, or outright malformed — every
    ///     one has to render the English front door rather than throw on the request path.
    /// </summary>
    [Theory]
    [InlineData("zz-ZZ")]
    [InlineData("de-DE,de;q=0.9")]
    [InlineData("zh-Hans-CN")]
    [InlineData("*")]
    [InlineData("")]
    [InlineData("---")]
    [InlineData("!!!;q=notanumber")]
    [InlineData("en-US;q=0.9,,,;;;")]
    public async Task AnUnplaceableAcceptLanguageStillRendersTheFrontDoor(string acceptLanguage)
    {
        var body = await GetFrontDoorWithLanguage(acceptLanguage);

        Assert.Contains(">Sign in<", body);
    }

    // "an exactly-supported tag is never re-regioned" is deliberately NOT an E2E fact: es-MX and
    // es-ES render the same string for every key on this page, so the assertion could not fail
    // for the reason it claimed. SupportedCulturesTests pins it where it is actually visible.

    private async Task<string> GetFrontDoorWithLanguage(string acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Welcome");
        // Without validation on purpose — half these headers are deliberately malformed, and
        // HttpClient would refuse to send what a real browser can absolutely put on the wire.
        request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
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
