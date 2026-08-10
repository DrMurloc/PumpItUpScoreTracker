using System.Net;
using ScoreTracker.Tests.E2E.Support;

namespace ScoreTracker.Tests.E2E;

/// <summary>
///     The resolution order through the real host (docs/design/culture-resolution.md).
///     <para>
///         Everything else about this feature is unit-testable, but not the two things that
///         actually broke it: where <c>UseRequestLocalization</c> sits in the pipeline, and which
///         index the provider was inserted at. Above <c>UseAuthentication</c> no provider can see
///         who is asking, and an <c>Add</c> where an <c>Insert</c> belongs demotes the account
///         setting below the cookie — both revert the feature with every fast suite green. Only a
///         real request with a real sign-in can tell.
///     </para>
///     <para>
///         Driven with HttpClient rather than Playwright on purpose: the shell renders as static
///         HTML before any circuit exists, so its nav labels are the earliest and most honest
///         evidence of the culture the request resolved to.
///     </para>
/// </summary>
[Collection("E2E")]
public sealed class CultureResolutionTests : IAsyncLifetime
{
    private const string SpanishBrowser = "es-ES,es;q=0.9";
    private const string EnglishNav = "My Progress";
    private const string SpanishNav = "Mi progreso";

    private readonly E2EAppFixture _fixture;
    private HttpClientHandler _handler = null!;
    private HttpClient _client = null!;

    public CultureResolutionTests(E2EAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        await _fixture.Seed.SeedPhoenixChartAsync("Conflict", 20, "Single");

        // A cookie jar of its own, so every test starts with no culture cookie — which is the
        // state a returning player is actually in, and the one the old code had no answer for.
        _handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = false
        };
        _client = new HttpClient(_handler) { BaseAddress = new Uri(_fixture.BaseUrl) };
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _handler.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     The reported bug, end to end: an account set to English, a Spanish browser, and no
    ///     culture cookie to bridge them. Before the saved setting was read on the request path
    ///     this rendered Spanish.
    /// </summary>
    [Fact]
    public async Task ASavedLanguageOutranksTheBrowser()
    {
        var userId = await _fixture.Seed.SeedUserAsync("CultureSaved");
        await _fixture.Seed.SeedCultureAsync(userId, "en-US");
        await SignInAsync(userId);

        var body = await GetAsync("/", SpanishBrowser);

        Assert.Contains(EnglishNav, body);
        Assert.DoesNotContain(SpanishNav, body);
    }

    /// <summary>
    ///     The control, and the half that must not regress: same page, same header, same signed-in
    ///     state — only the stored setting differs. Without one, the browser still decides.
    /// </summary>
    [Fact]
    public async Task WithoutASavedLanguageTheBrowserStillDecides()
    {
        var userId = await _fixture.Seed.SeedUserAsync("CultureUnset");
        await SignInAsync(userId);

        var body = await GetAsync("/", SpanishBrowser);

        Assert.Contains(SpanishNav, body);
    }

    /// <summary>
    ///     Rank 1 is a preview: it wins its own request and is never written down. Persisting it
    ///     is how a shared "?culture=" link used to change the recipient's language for good.
    /// </summary>
    [Fact]
    public async Task AQueryStringPreviewWinsOnceAndIsNotWrittenDown()
    {
        var userId = await _fixture.Seed.SeedUserAsync("CulturePreview");
        await _fixture.Seed.SeedCultureAsync(userId, "en-US");
        await SignInAsync(userId);

        using var preview = await RequestAsync("/?culture=es-ES", SpanishBrowser);
        Assert.Contains(SpanishNav, await preview.Content.ReadAsStringAsync());

        // Read off the wire, not out of the cookie jar: the cookie is Secure and the test host
        // speaks plain HTTP, so a jar that never held it looks identical to one the response
        // never tried to fill — the assertion would pass without proving anything.
        var setCookies = preview.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : Array.Empty<string>();
        Assert.DoesNotContain(setCookies, c => c.StartsWith(".AspNetCore.Culture", StringComparison.Ordinal));

        Assert.Contains(EnglishNav, await GetAsync("/", SpanishBrowser));
    }

    /// <summary>
    ///     A stale cookie is the state a signed-in player is left in after changing their
    ///     language on another device — it must lose to what the account says.
    /// </summary>
    [Fact]
    public async Task ASavedLanguageOutranksACookieLeftBehind()
    {
        var userId = await _fixture.Seed.SeedUserAsync("CultureStaleCookie");
        await _fixture.Seed.SeedCultureAsync(userId, "en-US");
        await SignInAsync(userId);
        _handler.CookieContainer.Add(new Cookie(".AspNetCore.Culture", "c%3Des-ES%7Cuic%3Des-ES", "/",
            new Uri(_fixture.BaseUrl).Host));

        var body = await GetAsync("/", SpanishBrowser);

        Assert.Contains(EnglishNav, body);
    }

    private async Task SignInAsync(Guid userId)
    {
        using var form = new FormUrlEncodedContent(new[]
            { new KeyValuePair<string, string>("userId", userId.ToString()) });

        var response = await _client.PostAsync("/Login/Dev", form);

        // The DevAuth backdoor redirects to wherever a developer belongs; the cookie is what
        // matters, and the jar has it either way.
        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Dev sign-in answered {response.StatusCode}");
    }

    private async Task<string> GetAsync(string path, string acceptLanguage)
    {
        using var response = await RequestAsync(path, acceptLanguage);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> RequestAsync(string path, string acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }
}
