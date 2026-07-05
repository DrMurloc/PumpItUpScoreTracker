using System.ComponentModel;
using MediatR;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;

namespace ScoreTracker.Web.Controllers.Api;

[ApiToken]
[Route("api/weeklyCharts")]
[EnableCors("API")]
public sealed class WeeklyChartsController : Controller
{
    private readonly IMediator _mediator;
    // IUserRepository goes at P6 when the Identity vertical publishes its reader.
    private readonly IUserRepository _users;

    public WeeklyChartsController(
        IMediator mediator,
        IUserRepository users)
    {
        _mediator = mediator;
        _users = users;
    }

    [HttpGet("scores")]
    public async Task<IActionResult> GetWeeklyChartScores([FromQuery(Name = "ChartIds")] string? chartIdString,
        [FromQuery(Name = "Mix")] [DefaultValue("Phoenix")]
        string? mixString = null)
    {
        if (!ApiMixParser.TryParse(mixString, out var mix))
            return BadRequest(ApiMixParser.InvalidMessage);

        var chartIds = chartIdString?.Split(",").Select(s => s.Trim()).Where(s => Guid.TryParse(s, out _))
            .Select(Guid.Parse).Distinct().ToHashSet();

        var entries = await _mediator.Send(new GetWeeklyChartEntriesQuery(Mix: mix), HttpContext.RequestAborted);
        if (chartIds != null) entries = entries.Where(e => chartIds.Contains(e.ChartId));

        var finalEntries = entries.ToArray();
        var charts =
            (await _mediator.Send(
                new GetChartsQuery(mix, ChartIds: finalEntries.Select(e => e.ChartId)),
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
        [FromQuery(Name = "LevelMax")] int? maxInt = null,
        [FromQuery(Name = "Mix")] [DefaultValue("Phoenix")]
        string? mixString = null)
    {
        if (chartTypeString != null && !Enum.TryParse<ChartType>(chartTypeString, out _))
            return BadRequest($"Invalid Chart Type. Options are: {string.Join(',', Enum.GetValues<ChartType>())}");

        if (minInt != null && !DifficultyLevel.IsValid(minInt.Value))
            return BadRequest(
                $"Minimum Difficulty Level must be between {DifficultyLevel.Min} and {DifficultyLevel.Max}");

        if (maxInt != null && !DifficultyLevel.IsValid(maxInt.Value))
            return BadRequest(
                $"Maximum Difficulty Level must be between {DifficultyLevel.Min} and {DifficultyLevel.Max}");

        if (!ApiMixParser.TryParse(mixString, out var mix))
            return BadRequest(ApiMixParser.InvalidMessage);

        // Each mix runs its own weekly board (locked decision) — this reads the requested mix's.
        var chartIds = (await _mediator.Send(new GetWeeklyChartsQuery(mix), HttpContext.RequestAborted))
            .Select(w => w.ChartId);
        var charts =
            await _mediator.Send(new GetChartsQuery(mix, ChartIds: chartIds),
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