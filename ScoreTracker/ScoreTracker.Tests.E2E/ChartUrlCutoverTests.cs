using System.Net;
using ScoreTracker.Tests.E2E.Support;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The chart-page URL cutover (docs/design/chart-details-overhaul.md): pretty vanity URLs
///     serve real HTML, the GUID permalink and its legacy aliases 301 to canonical forever,
///     and a non-canonical slug (here, the wrong casing) 301s to the one lowercase URL. These
///     run through the real host with auto-redirect OFF, so each 301 is asserted directly —
///     the redirect status is the SEO signal, and a static component that 302'd instead would
///     silently fail to consolidate.
/// </summary>
[Collection("E2E")]
public sealed class ChartUrlCutoverTests : IAsyncLifetime
{
    private const string Canonical = "/Charts/phoenix/conflict/s20";

    private readonly E2EAppFixture _fixture;
    private HttpClient _client = null!;
    private Guid _chartId;

    public ChartUrlCutoverTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _chartId = await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 20, "Single");
        // Auto-redirect off: assert the 301 itself, not where it lands.
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(_fixture.BaseUrl)
        };
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task TheCanonicalVanityUrlServesRealHtmlPreCircuit()
    {
        var response = await _client.GetAsync(Canonical);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // The hero rendered server-side — the song is an <h1>, before any circuit.
        Assert.Contains("chart-hero-title", body);
        Assert.Contains("Conflict", body);
    }

    [Fact]
    public async Task TheGuidPermalink301sToCanonical()
    {
        var response = await _client.GetAsync($"/Chart/{_chartId}");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(Canonical, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task LegacyChartAndRecordAliases301ToTheCatalog()
    {
        foreach (var alias in new[] { "/Chart", "/Record" })
        {
            var response = await _client.GetAsync(alias);
            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal("/Charts", response.Headers.Location?.OriginalString);
        }
    }

    [Fact]
    public async Task ANonCanonicalCasing301sToTheLowercaseCanonical()
    {
        // The page issues this one itself (a historical/stale slug shares the canonical
        // route's shape, so it can't live in the permalink controller) — the proof that a
        // static SSR page can emit a real 301, not a 302.
        var response = await _client.GetAsync("/Charts/Phoenix/Conflict/S20");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(Canonical, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AnUnknownChartTripleAnswers404()
    {
        var response = await _client.GetAsync("/Charts/phoenix/no-such-song/s20");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
