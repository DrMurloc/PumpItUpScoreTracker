using MediatR;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api;

[ApiToken]
[Route("api/weeklyCharts")]
[EnableCors("API")]
public sealed class WeeklyChartsController : Controller
{
    private readonly IMediator _mediator;
    private readonly IWeeklyTournamentRepository _weeklyCharts;
    private readonly IUserRepository _users;

    public WeeklyChartsController(
        IMediator mediator,
        IWeeklyTournamentRepository weeklyCharts,
        IUserRepository users)
    {
        _mediator = mediator;
        _weeklyCharts = weeklyCharts;
        _users = users;
    }

    [HttpGet("scores")]
    public async Task<IActionResult> GetWeeklyChartScores([FromQuery(Name = "ChartIds")] string? chartIdString)
    {
        var chartIds = chartIdString?.Split(",").Select(s => s.Trim()).Where(s => Guid.TryParse(s, out _))
            .Select(Guid.Parse).Distinct().ToHashSet();

        var entries = await _weeklyCharts.GetEntries(null, HttpContext.RequestAborted);
        if (chartIds != null) entries = entries.Where(e => chartIds.Contains(e.ChartId));

        var finalEntries = entries.ToArray();
        var charts =
            (await _mediator.Send(
                new GetChartsQuery(MixEnum.Phoenix, ChartIds: finalEntries.Select(e => e.ChartId)),
                HttpContext.RequestAborted))
            .ToDictionary(c => c.Id);
        var users = (await _users.GetUsers(finalEntries.Select(e => e.UserId), HttpContext.RequestAborted))
            .ToDictionary(u => u.Id);

        return Ok(finalEntries.Select(e => new PlayerChartScoreDto
        {
            ChartId = e.ChartId,
            Player = new PlayerDto(users[e.UserId]),
            Score = new ScoreDto(e.Score, e.Plate, e.IsBroken)
        }));
    }

    [HttpGet]
    public async Task<IActionResult> GetWeeklyCharts(
        [FromQuery(Name = "ChartType")] string? chartTypeString = null,
        [FromQuery(Name = "LevelMin")] int? minInt = null,
        [FromQuery(Name = "LevelMax")] int? maxInt = null)
    {
        if (chartTypeString != null && !Enum.TryParse<ChartType>(chartTypeString, out _))
            return BadRequest($"Invalid Chart Type. Options are: {string.Join(',', Enum.GetValues<ChartType>())}");

        if (minInt != null && !DifficultyLevel.IsValid(minInt.Value))
            return BadRequest(
                $"Minimum Difficulty Level must be between {DifficultyLevel.Min} and {DifficultyLevel.Max}");

        if (maxInt != null && !DifficultyLevel.IsValid(maxInt.Value))
            return BadRequest(
                $"Maximum Difficulty Level must be between {DifficultyLevel.Min} and {DifficultyLevel.Max}");


        var chartIds = (await _weeklyCharts.GetWeeklyCharts(HttpContext.RequestAborted)).Select(w => w.ChartId);
        var charts =
            await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix, ChartIds: chartIds),
                HttpContext.RequestAborted);

        if (chartTypeString != null)
        {
            var chartType = Enum.Parse<ChartType>(chartTypeString);
            charts = charts.Where(c => c.Type == chartType);
        }

        if (minInt != null) charts = charts.Where(c => c.Level >= minInt.Value);

        if (maxInt != null) charts = charts.Where(c => c.Level <= maxInt.Value);

        return Ok(charts.OrderBy(c => c.Level).ThenBy(c => c.Type).Select(c => new ChartDto(c)));
    }
}