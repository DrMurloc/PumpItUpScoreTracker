using System.Globalization;
using System.Threading.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.Web.Middleware;

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
///     <para>
///         Both tiers sit at the same ceiling because the heaviest honest job on a personal token
///         is not a trickle of reads — it is a full catalog pull, which is every chart and song and
///         tier list of all 31 mixes and lands as roughly 500 requests back to back. The two
///         constants stay separate so the tiers can diverge again without reshaping the policy.
///     </para>
/// </summary>
public static class ApiV2RateLimiting
{
    public const string PolicyName = "ApiV2";

    private const int ToolRequestsPerMinute = 600;
    private const int PersonalRequestsPerMinute = 600;

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

        options.OnRejected = async (context, cancellationToken) =>
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

            // Counted after the fact, under the key's name. The scheme has not run — this hook sits
            // above authorization — so the credential is all there is to name the caller by, and
            // the vertical answers with the principal it resolved on the calls that got through.
            var credential = ApiCredential.Parse(context.HttpContext.Request.Headers.Authorization.ToString());
            ToolKeyPrincipal? principal = null;
            if (credential.Failure is null)
            {
                var mediator = context.HttpContext.RequestServices.GetRequiredService<IMediator>();
                principal = await mediator.Send(new RecordRateLimitedRequestCommand(credential.Secret),
                    cancellationToken);
            }

            // The request log's line for this request: the middleware that writes it sits below
            // this hook and never sees a rejection.
            ApiRequestLogMiddleware.LogRejected(
                context.HttpContext.RequestServices.GetRequiredService<ILogger<ApiRequestLogMiddleware>>(),
                context.HttpContext, credential, principal);
        };

        return options;
    }
}
