using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.Web.Services.MoM;

namespace ScoreTracker.Web.Controllers;

/// <summary>
///     The retired March of Murlocs routes 301 onto the dated URL family
///     (march-of-murlocs.md §11.1) — every old link, bookmark and Discord unfurl keeps
///     resolving. A board's Guid is its legacy tournament's Guid (Slice 2), which is what
///     lets the old board and record URLs land on the right season. A real MVC 301, so the
///     signals consolidate.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class MoMPermalinkController : Controller
{
    // The tournament directory: the live season IS the landing page now.
    [HttpGet("/Tournaments/MarchOfMurlocs")]
    public IActionResult Directory()
    {
        return RedirectPermanent(MoMRoutes.Root);
    }

    [HttpGet("/Tournament/Stamina/{id:guid}")]
    [HttpGet("/Tournament/Stamina/{id:guid}/Record")]
    public async Task<IActionResult> Board(Guid id, [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var board = await mediator.Send(new GetMoMBoardQuery(id), cancellationToken);
        return board == null
            ? RedirectPermanent(MoMRoutes.Root)
            : RedirectPermanent(MoMRoutes.BoardPath(board.Season, board.ChartType));
    }

    // The "Test Scores" planner and its older alias: the Planner is the future tense now.
    [HttpGet("/SessionBuilder")]
    [HttpGet("/TournamentBuilder")]
    public IActionResult Planner()
    {
        return RedirectPermanent(MoMRoutes.PlannerPath);
    }
}
