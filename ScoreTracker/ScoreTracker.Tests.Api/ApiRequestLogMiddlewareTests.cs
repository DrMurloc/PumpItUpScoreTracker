using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.Web.Middleware;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Tests.Api;

/// <summary>
///     The one line per API request that App Insights turns into columns. What matters is that
///     every field is present under its name, the tier follows the claims, the route is the
///     template rather than the URL, and nothing outside /api/ is logged at all.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ApiRequestLogMiddlewareTests
{
    private static readonly Guid AToolId = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
    private static readonly Guid AUserId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");

    /// <summary>Captures the structured state of every line, by placeholder name.</summary>
    private sealed class CapturingLogger : ILogger<ApiRequestLogMiddleware>
    {
        public List<Dictionary<string, object?>> Lines { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var fields = ((IReadOnlyList<KeyValuePair<string, object?>>)state!)
                .ToDictionary(p => p.Key, p => p.Value);
            fields["{Level}"] = logLevel;
            fields["{Message}"] = formatter(state, exception);
            Lines.Add(fields);
        }
    }

    private static DefaultHttpContext Request(string path, string? template, ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = path;
        if (user is not null) context.User = user;
        if (template is not null)
            context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(template),
                0, EndpointMetadataCollection.Empty, template));
        return context;
    }

    private static ClaimsPrincipal Tool()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ToolKeyAuthenticationScheme.ToolIdClaim, AToolId.ToString()),
            new Claim(ToolKeyAuthenticationScheme.KeyNameClaim, "production")
        }, "ApiV2"));
    }

    private static ClaimsPrincipal Person()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, AUserId.ToString()),
            new Claim(ToolKeyAuthenticationScheme.PersonalTokenClaim, "true")
        }, "ApiToken"));
    }

    /// <summary>The cookie principal: the same site claims, without the mark a token leaves.</summary>
    private static ClaimsPrincipal Browser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, AUserId.ToString())
        }, "External"));
    }

    private static async Task<Dictionary<string, object?>> Run(DefaultHttpContext context, int status = 200)
    {
        var logger = new CapturingLogger();
        var middleware = new ApiRequestLogMiddleware(ctx =>
        {
            ctx.Response.StatusCode = status;
            return Task.CompletedTask;
        }, logger);

        await middleware.InvokeAsync(context);

        return Assert.Single(logger.Lines);
    }

    [Fact]
    public async Task AToolsCallIsLoggedUnderItsToolAndKeyWithTheRouteTemplate()
    {
        var line = await Run(Request("/api/v2/players/" + AUserId + "/scores", "api/v2/players/{id}/scores", Tool()));

        Assert.Equal("tool", line["Tier"]);
        Assert.Equal(AToolId, line["ToolId"]);
        Assert.Equal("production", line["KeyName"]);
        Assert.Null(line["UserId"]);
        Assert.Equal("GET", line["Method"]);
        Assert.Equal("api/v2/players/{id}/scores", line["Route"]);
        Assert.Equal(200, line["Status"]);
        Assert.IsType<long>(line["DurationMs"]);
        Assert.Equal(LogLevel.Information, line["{Level}"]);
        Assert.StartsWith("ApiRequest ", (string)line["{Message}"]!);
    }

    [Fact]
    public async Task APersonalTokensCallIsLoggedUnderItsUser()
    {
        var line = await Run(Request("/api/v2/players/me", "api/v2/players/me", Person()));

        Assert.Equal("personal", line["Tier"]);
        Assert.Null(line["ToolId"]);
        Assert.Null(line["KeyName"]);
        Assert.Equal(AUserId, line["UserId"]);
    }

    /// <summary>
    ///     The admin's own browser on /api/admin, or a signed-in Swagger try-out without a token,
    ///     is a session: not the personal tier, and never the person's id in the trace.
    /// </summary>
    [Fact]
    public async Task ASignedInBrowserOnAnApiRouteIsASessionNotAPerson()
    {
        var line = await Run(Request("/api/admin/scoreBatches", "api/admin/scoreBatches", Browser()));

        Assert.Equal("session", line["Tier"]);
        Assert.Null(line["UserId"]);
        Assert.Null(line["ToolId"]);
    }

    /// <summary>No claims at all — a 401 in flight, or a public endpoint — is still a line.</summary>
    [Fact]
    public async Task AnAnonymousCallIsLoggedWithItsStatus()
    {
        var line = await Run(Request("/api/v2/charts", "api/v2/charts"), 401);

        Assert.Equal("anonymous", line["Tier"]);
        Assert.Null(line["ToolId"]);
        Assert.Null(line["UserId"]);
        Assert.Equal(401, line["Status"]);
    }

    [Fact]
    public async Task ARequestThatMatchedNoEndpointNeverLogsTheUrl()
    {
        var line = await Run(Request("/api/v9/secret-player-name", null), 404);

        Assert.Equal("(unmatched)", line["Route"]);
    }

    [Fact]
    public async Task NothingOutsideTheApiIsLogged()
    {
        var logger = new CapturingLogger();
        var reached = false;
        var middleware = new ApiRequestLogMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        }, logger);

        await middleware.InvokeAsync(Request("/TierLists/Singles/20", "TierLists/{type}/{level}", Person()));

        Assert.True(reached);
        Assert.Empty(logger.Lines);
    }

    [Fact]
    public async Task ARejectionIsLoggedAsA429UnderTheKeyTheVerticalNamed()
    {
        var logger = new CapturingLogger();
        var context = Request("/api/v2/charts", "api/v2/charts");

        ApiRequestLogMiddleware.LogRejected(logger, context, ApiCredential.Parse("Bearer piu_scores_live_x"),
            new ToolKeyPrincipal(AToolId, "production"));
        ApiRequestLogMiddleware.LogRejected(logger, context,
            ApiCredential.Parse("Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("x:" + Guid.NewGuid()))),
            null);
        ApiRequestLogMiddleware.LogRejected(logger, context, ApiCredential.Parse("Digest nope"), null);

        Assert.Equal(3, logger.Lines.Count);
        Assert.All(logger.Lines, l => Assert.Equal(429, l["Status"]));
        Assert.Equal(new object?[] { "tool", "personal", "anonymous" }, logger.Lines.Select(l => l["Tier"]));
        Assert.Equal("production", logger.Lines[0]["KeyName"]);
        Assert.Null(logger.Lines[1]["UserId"]);
    }
}
