using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.Web.Services;

namespace ScoreTracker.Web.Controllers;

/// <summary>
///     The old March of Murlocs routes, retired with their pages in Slice 4a
///     (docs/design/march-of-murlocs.md §11.1): the tournament directory becomes the season page,
///     and a board page becomes the season it belongs to with that board selected. A board's id is
///     its legacy tournament id, so every link ever shared keeps resolving. A stamina id that is not
///     a MoM board — a tournament on the legacy table — lands on that tournament's own page.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class MoMRedirectController : Controller
{
    private readonly IMediator _mediator;

    public MoMRedirectController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("/Tournaments/MarchOfMurlocs")]
    public IActionResult Directory()
    {
        return RedirectPermanent(MoMText.SeasonRoute);
    }

    [HttpGet("/Tournament/Stamina/{id:guid}")]
    public async Task<IActionResult> Board(Guid id, CancellationToken cancellationToken)
    {
        var locator = await _mediator.Send(new GetMoMBoardLocatorQuery(id), cancellationToken);
        if (locator == null) return RedirectPermanent($"/Tournament/{id}/Qualifiers");
        var season = locator.IsLive ? MoMText.SeasonRoute : MoMText.SeasonPath(locator.SeasonId);
        return RedirectPermanent($"{season}?board={locator.ChartType}");
    }
}
