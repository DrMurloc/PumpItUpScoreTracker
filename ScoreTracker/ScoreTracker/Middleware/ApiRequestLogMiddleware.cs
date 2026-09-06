using System.Diagnostics;
using System.Security.Claims;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Middleware;

/// <summary>
///     One structured log line per request under <c>/api/</c> — tier, tool, key name, route,
///     status, duration — which is how API usage reaches App Insights
///     (docs/design/api-usage-telemetry.md). Every field is a named placeholder, so each is a
///     column there rather than a word in a sentence.
///     <para>
///         Sits between the rate limiter and authorization. Below authorization it would never see
///         a 401, because that middleware answers those itself; above the limiter it would see the
///         429s but never the principal. So the limiter's rejection hook writes the same line for
///         a 429 through <see cref="LogRejected" />, and the two cannot drift apart.
///     </para>
///     <para>
///         Never the credential. The tool tier logs the key's name, the personal tier the user's
///         id, and the route is the endpoint's template rather than the URL that was asked for.
///     </para>
/// </summary>
public sealed partial class ApiRequestLogMiddleware
{
    /// <summary>
    ///     The status a request the client abandoned is logged under. Not one the caller ever
    ///     receives — nobody is listening — but the conventional "client closed request", so the
    ///     trace can tell a hang-up from a crash.
    /// </summary>
    public const int ClientClosedRequest = 499;

    private static readonly PathString ApiPrefix = new("/api");

    private readonly ILogger<ApiRequestLogMiddleware> _logger;
    private readonly RequestDelegate _next;

    public ApiRequestLogMiddleware(RequestDelegate next, ILogger<ApiRequestLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(ApiPrefix))
        {
            await _next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        catch
        {
            // The status on the context is whatever was set before the throw, which is nothing
            // useful. A client that hung up mid-response surfaces here too — a bulk pull against a
            // short client timeout — and that is not a server error; anything else is the 500 the
            // exception handler is about to produce.
            var status = context.RequestAborted.IsCancellationRequested
                ? ClientClosedRequest
                : StatusCodes.Status500InternalServerError;
            Log(_logger, context, status, Elapsed(started));
            throw;
        }

        Log(_logger, context, context.Response.StatusCode, Elapsed(started));
    }

    /// <summary>
    ///     The rate limiter's line. The scheme has not run, so the caller is named from the
    ///     credential it presented and the principal the vertical resolved on the calls that got
    ///     through: a resolved key is the tool tier, a personal token is the personal tier with no
    ///     user to name, and anything else is anonymous — an unknown key is nobody.
    /// </summary>
    public static void LogRejected(ILogger logger, HttpContext context, ApiCredential credential,
        ToolKeyPrincipal? principal)
    {
        var tier = principal is not null ? "tool"
            : credential.Failure is null && Guid.TryParse(credential.Secret, out _) ? "personal"
            : "anonymous";

        ApiRequest(logger, tier, Text(principal?.ToolId), principal?.KeyName ?? string.Empty, string.Empty,
            context.Request.Method, RouteOf(context), StatusCodes.Status429TooManyRequests, 0);
    }

    private static void Log(ILogger logger, HttpContext context, int status, long durationMs)
    {
        var user = context.User;
        var toolId = Parse(user.FindFirstValue(ToolKeyAuthenticationScheme.ToolIdClaim));
        var personal = toolId is null && user.HasClaim(c => c.Type == ToolKeyAuthenticationScheme.PersonalTokenClaim);
        var userId = personal ? Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)) : null;
        // A signed-in browser reaching an API route carries the cookie principal, not a token —
        // the admin's own page today, a Swagger try-out tomorrow. Its own tier, never the person's id.
        var tier = toolId is not null ? "tool"
            : personal ? "personal"
            : user.Identity?.IsAuthenticated == true ? "session"
            : "anonymous";

        ApiRequest(logger, tier, Text(toolId),
            toolId is null ? string.Empty : user.FindFirstValue(ToolKeyAuthenticationScheme.KeyNameClaim) ?? string.Empty,
            Text(userId), context.Request.Method, RouteOf(context), status, durationMs);
    }

    /// <summary>
    ///     An absent field is an empty string, never a null: the exporter between this line and the
    ///     query is nobody's to inspect, and `isnotempty()` reads an empty string the same way
    ///     whatever a null would have become.
    /// </summary>
    private static string Text(Guid? id)
    {
        return id?.ToString() ?? string.Empty;
    }

    /// <summary>The template, never the URL: a player's id is not a column anyone needs.</summary>
    private static string RouteOf(HttpContext context)
    {
        return (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
    }

    private static Guid? Parse(string? claim)
    {
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static long Elapsed(long startedTimestamp)
    {
        return (long)Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
    }

    [LoggerMessage(EventId = 4200, Level = LogLevel.Information,
        Message = "ApiRequest {Tier} {ToolId} {KeyName} {UserId} {Method} {Route} {Status} {DurationMs}")]
    private static partial void ApiRequest(ILogger logger, string tier, string toolId, string keyName,
        string userId, string method, string route, int status, long durationMs);
}
