using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ScoreTracker.Web.Security;

/// <summary>
///     api/v2's rate limit, partitioned by credential.
///     <para>
///         Measured against real usage, these ceilings are generous on purpose: there are a few
///         hundred active players, so a tool sweeping every one of them makes hundreds of requests
///         rather than tens of thousands. The limit exists to stop a runaway loop, not to ration
///         honest traffic.
///     </para>
///     <para>
///         Partitioned by the Authorization header rather than by IP: a tool is one caller wherever
///         it runs from, and several makers behind one cloud NAT are not each other's problem.
///     </para>
/// </summary>
public static class ApiV2RateLimiting
{
    public const string PolicyName = "ApiV2";

    private const int ToolRequestsPerMinute = 600;
    private const int PersonalRequestsPerMinute = 60;

    public static RateLimiterOptions AddApiV2Policy(this RateLimiterOptions options)
    {
        options.AddPolicy(PolicyName, context =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            var isTool = header.StartsWith("Bearer ", StringComparison.Ordinal);
            var permit = isTool ? ToolRequestsPerMinute : PersonalRequestsPerMinute;

            // An unauthenticated request has no credential to partition on; it shares one bucket,
            // which is the right shape because it is about to be rejected anyway.
            var key = string.IsNullOrEmpty(header) ? "anonymous" : header;

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
        });

        options.OnRejected = (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            // Retry-After and the RateLimit-* family are what let a well-behaved client back off
            // on its own instead of hammering and guessing.
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var seconds = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                context.HttpContext.Response.Headers.RetryAfter = seconds;
                context.HttpContext.Response.Headers["RateLimit-Reset"] = seconds;
            }

            context.HttpContext.Response.Headers["RateLimit-Remaining"] = "0";
            return ValueTask.CompletedTask;
        };

        return options;
    }
}
