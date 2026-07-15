using System.Net;
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

    public NonComponentEndpointTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 20, "Single");
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

    [Fact]
    public async Task TheSitemapStillServesXml()
    {
        var response = await _client.GetAsync("/sitemap.xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<urlset", body);
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
