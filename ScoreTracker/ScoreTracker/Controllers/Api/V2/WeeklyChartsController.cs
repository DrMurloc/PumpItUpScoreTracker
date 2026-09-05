using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     The weekly challenge board. Each mix runs its own board, so <c>mix</c> selects which.
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/weekly-charts")]
public sealed class WeeklyChartsController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserRepository _users;

    public WeeklyChartsController(IMediator mediator, IUserRepository users)
    {
        _mediator = mediator;
        _users = users;
    }

    /// <summary>The charts on this week's board.</summary>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>; each mix runs its own board.</param>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPageDto<WeeklyChartDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var rows = (await _mediator.Send(new GetWeeklyChartsQuery(mix)))
            .Select(w => new WeeklyChartDto { ChartId = w.ChartId })
            .ToArray();

        return Json(Page(rows, rows.Length, rows.Length, null));
    }

    /// <summary>Every player's entry on the board.</summary>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>; each mix runs its own board.</param>
    [HttpGet("scores")]
    [ProducesResponseType(typeof(CursorPageDto<WeeklyChartScoreDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetScores([FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var entries = (await _mediator.Send(new GetWeeklyChartEntriesQuery(Mix: mix))).ToArray();
        var users = (await _users.GetUsers(entries.Select(e => e.UserId), HttpContext.RequestAborted))
            .ToDictionary(u => u.Id);

        var rows = entries.Select(e => new WeeklyChartScoreDto
        {
            ChartId = e.ChartId,
            UserId = e.UserId,
            Username = users.TryGetValue(e.UserId, out var user) ? user.Name.ToString() : string.Empty,
            Score = e.Score,
            // Already null on a broken entry — the game awards no plate for a failed stage.
            Plate = e.Plate is null ? null : PhoenixPlateHelperMethods.GetName(e.Plate.Value),
            IsBroken = e.IsBroken
        }).ToArray();

        return Json(Page(rows, rows.Length, rows.Length, null));
    }
}
