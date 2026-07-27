using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ScoreTracker.Tests.E2E.Support;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     What the browser is actually told to cache. The unit ratchet
///     (<c>StaticAssetVersioningTests</c>) can only see that the markup says
///     <c>@Assets["css/site.css"]</c>; whether that resolves to a hashed name, and what headers
///     come back when the browser asks for it, are facts of a hosted run against a real build
///     manifest. The failure this guards is silent and slow: @Assets hands back the plain path
///     when the manifest is missing, every page still renders, and the only symptom is a
///     player on the next release looking at last release's stylesheet.
/// </summary>
[Collection("E2E")]
public sealed class StaticAssetCacheTests : IAsyncLifetime
{
    // css/site.ivd8qpcnra.css — the fingerprint MapStaticAssets derives from file content.
    private static readonly Regex HashedSiteCss = new(@"css/site\.[a-z0-9]+\.css", RegexOptions.Compiled);
    private static readonly Regex HashedChartsCss = new(@"css/charts\.[a-z0-9]+\.css", RegexOptions.Compiled);
    private static readonly Regex HashedNavJs = new(@"js/nav\.[a-z0-9]+\.js", RegexOptions.Compiled);
    // /css/front-door.css?v=<hash> — the TagHelper's content hash, the Razor Page equivalent.
    private static readonly Regex VersionedFrontDoorCss =
        new(@"css/front-door\.css\?v=[A-Za-z0-9_\-]+", RegexOptions.Compiled);
    private static readonly Regex HashedHelpersJs = new(@"js/helpers\.[a-z0-9]+\.js", RegexOptions.Compiled);

    private readonly E2EAppFixture _fixture;
    private HttpClient _client = null!;

    public StaticAssetCacheTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        // An empty database sends every route to the dev-populate harness, which serves none of
        // the pages this asserts against. One chart is enough to make the app look inhabited.
        await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 20, "Single");
        _client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     The shell's own stylesheets and scripts. Asserted against the raw body because the
    ///     URL in the markup is the whole mechanism — a rendered DOM would look identical
    ///     either way.
    /// </summary>
    [Fact]
    public async Task TheShellAsksForContentHashedCssAndJs()
    {
        var html = await _client.GetStringAsync("/TierLists");

        Assert.Matches(HashedSiteCss, html);
        Assert.Matches(HashedChartsCss, html);
        Assert.Matches(HashedNavJs, html);
        // The plain names are what a browser caches across a release, and the hand-bumped
        // version query is the thing that has to be remembered — neither should survive.
        Assert.DoesNotContain("\"/css/site.css\"", html);
        Assert.DoesNotContain("nav.js?v=", html);
    }

    /// <summary>
    ///     The front door is a Razor Page, where @Assets is not reachable — the asset
    ///     collection belongs to the component endpoint and is not in DI, so a page that
    ///     injects it throws on render. Its version rides the query instead. Both halves are
    ///     asserted: that the page serves at all, and that its stylesheet carries a hash.
    /// </summary>
    [Fact]
    public async Task TheFrontDoorVersionsItsStylesheet()
    {
        var response = await _client.GetAsync("/Welcome");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The tag rather than the whole document, so a failure reads as the markup that shipped.
        var link = Regex.Match(html, "<link[^>]*front-door[^>]*>").Value;
        Assert.True(VersionedFrontDoorCss.IsMatch(link),
            $"The front door's stylesheet shipped as '{link}' (served from {response.RequestMessage?.RequestUri}) — it needs a version, or a browser keeps the previous release's copy.");
    }

    /// <summary>
    ///     Both names serve, and the plain one revalidates. The plain name is the path fonts and
    ///     images take when CSS reaches for them by url(), so it has to keep working and it has
    ///     to keep checking. No assertion here about the year-long immutable cache: this host
    ///     runs Development, where MapStaticAssets deliberately answers no-cache for every asset
    ///     so a developer is not fighting their own browser. What production sends is a property
    ///     of the shipped manifest, pinned below.
    /// </summary>
    [Fact]
    public async Task BothTheHashedAndPlainNamesServeAndThePlainOneRevalidates()
    {
        var html = await _client.GetStringAsync("/TierLists");
        var hashed = HashedSiteCss.Match(html).Value;

        var hashedResponse = await _client.GetAsync($"/{hashed}");
        Assert.Equal(HttpStatusCode.OK, hashedResponse.StatusCode);

        var plainResponse = await _client.GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, plainResponse.StatusCode);
        Assert.True(plainResponse.Headers.CacheControl?.NoCache,
            $"/css/site.css came back with Cache-Control: {plainResponse.Headers.CacheControl?.ToString() ?? "(none)"} — assets reached by their plain name have to revalidate, or a stale font or image outlives the release that changed it.");
        Assert.NotNull(plainResponse.Headers.ETag);
    }

    /// <summary>
    ///     The headers production actually sends, read off the build manifest that ships next to
    ///     the app — the one artifact that says the same thing in every environment. A hashed
    ///     name may be cached for a year precisely because changing the file changes the name;
    ///     a plain one may not.
    /// </summary>
    [Fact]
    public void TheManifestCachesHashedNamesForeverAndPlainNamesNotAtAll()
    {
        var manifest = Path.Combine(AppContext.BaseDirectory, "ScoreTracker.Web.staticwebassets.endpoints.json");
        Assert.True(File.Exists(manifest),
            $"No asset manifest at {manifest} — MapStaticAssets has nothing to serve from and @Assets falls back to unhashed paths.");

        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var routes = document.RootElement.GetProperty("Endpoints").EnumerateArray()
            .Select(e => (Route: e.GetProperty("Route").GetString()!, CacheControl: CacheControlOf(e)))
            .ToArray();

        var hashed = routes.Where(r => Regex.IsMatch(r.Route, @"^css/site\.[a-z0-9]+\.css$")).ToArray();
        Assert.NotEmpty(hashed);
        Assert.All(hashed, r => Assert.Equal("max-age=31536000, immutable", r.CacheControl));

        var plain = routes.Where(r => r.Route == "css/site.css").ToArray();
        Assert.NotEmpty(plain);
        Assert.All(plain, r => Assert.Equal("no-cache", r.CacheControl));
    }

    private static string? CacheControlOf(JsonElement endpoint)
    {
        foreach (var header in endpoint.GetProperty("ResponseHeaders").EnumerateArray())
            if (header.GetProperty("Name").GetString() == "Cache-Control")
                return header.GetProperty("Value").GetString();
        return null;
    }

    /// <summary>
    ///     The three JS modules the circuit imports at runtime have no tag carrying a hashed
    ///     name; the import map is what redirects them, and it only helps if it ships.
    /// </summary>
    [Fact]
    public async Task TheImportMapRedirectsTheRuntimeModuleImports()
    {
        var html = await _client.GetStringAsync("/TierLists");

        Assert.Contains("importmap", html);
        Assert.Matches(HashedHelpersJs, html);
    }
}
