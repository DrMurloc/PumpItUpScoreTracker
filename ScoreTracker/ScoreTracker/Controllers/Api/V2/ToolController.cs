using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     What the calling tool is, and what it is allowed right now.
///     <para>
///         Two things a maker cannot otherwise discover programmatically: how much quota is left, and
///         when their key dies. Since there are no expiry emails, this is the only warning a maker's
///         own healthcheck can assert on.
///     </para>
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/tool")]
public sealed class ToolController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public ToolController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var toolId = User.ToolId();
        if (toolId is null)
            return Problem("not-a-tool", "This endpoint is for tool keys.",
                detail: "A personal token has no tool. Create one on /Developers.");

        var tool = await _mediator.Send(new GetToolQuery(toolId.Value));
        if (tool is null) return NotFoundProblem("That tool no longer exists.");

        return Json(new
        {
            toolId = tool.Id,
            name = tool.Name,
            visibility = tool.Visibility.ToString(),
            webhookMode = tool.WebhookMode.ToString(),
            mixes = tool.Mixes.Select(m => m.ToString()).ToArray(),
            connectedPlayers = tool.ConnectedPlayers,
            acceptsAllToolsShare = tool.AcceptsAllToolsShare
        });
    }

    /// <summary>
    ///     The delivery feed as a pull — the same rows the webhook queue writes.
    ///     <para>
    ///         Free to build, because the table exists for the activity console regardless. It is the
    ///         honest answer for a maker on a laptop with no public URL: webhooks become an
    ///         optimisation rather than a prerequisite.
    ///     </para>
    /// </summary>
    /// <param name="after">The last delivery id you processed. Omit to start from the oldest held.</param>
    [HttpGet("/" + RoutePrefix + "/events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery(Name = "after")] string? after = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        var toolId = User.ToolId();
        if (toolId is null)
            return Problem("not-a-tool", "This endpoint is for tool keys.",
                detail: "A personal token has no delivery feed.");

        var rows = await _mediator.Send(new GetToolDeliveryFeedQuery(toolId.Value, after,
            Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit)));

        return Json(new
        {
            data = rows.Select(r => new
            {
                deliveryId = r.DeliveryId,
                sentAt = r.SentAt,
                mode = r.Mode,
                userId = r.UserId,
                mix = r.Mix,
                // Null once the body has aged out; the row stays so the sequence has no holes.
                body = r.Body
            }).ToArray()
        });
    }
}
