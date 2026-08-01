using MediatR;
using Microsoft.AspNetCore.Mvc;
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
}
