using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.ScoreCalculator;

namespace ScoreTracker.Web.Controllers;

/// <summary>
///     The score calculator's "load one of your plays" feed — a signed-in UI-support endpoint
///     (the /Charts/Export.csv family, not api/*), so the static page can fill its dialog
///     without running a circuit (docs/design/phoenix-score-calculator.md D7). Rows are the
///     caller's own judgement-carrying journal entries, display-ready: the chart's name,
///     bubble and jacket ride along so the script renders without a second call.
/// </summary>
[Authorize]
public class ScoreCalculatorPlaysController : Controller
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public ScoreCalculatorPlaysController(IMediator mediator, ICurrentUserAccessor currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("PhoenixCalculator/MyPlays")]
    public async Task<IActionResult> MyPlays([FromQuery] string? mix, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MixEnum>(mix, true, out var parsed) ||
            !ScoreCalculatorMixes.All.Contains(parsed))
            return BadRequest();

        var plays = await _mediator.Send(
            new GetJudgedPlaysQuery(_currentUser.User.Id, parsed), cancellationToken);
        var charts = (await _mediator.Send(new GetChartsQuery(parsed), cancellationToken))
            .ToDictionary(c => c.Id);

        return Json(plays
            .Where(play => play.Judgements != null && charts.ContainsKey(play.ChartId))
            .Select(play =>
            {
                var chart = charts[play.ChartId];
                return new
                {
                    song = chart.Song.Name.ToString(),
                    type = chart.Type.ToString(),
                    difficulty = chart.DifficultyString,
                    jacket = chart.Song.ImagePath.ToString(),
                    perfects = play.Judgements!.Perfects,
                    greats = play.Judgements.Greats,
                    goods = play.Judgements.Goods,
                    bads = play.Judgements.Bads,
                    misses = play.Judgements.Misses,
                    combo = play.Judgements.MaxCombo ?? 0,
                    isBroken = play.IsBroken
                };
            }));
    }
}
