using System.ComponentModel;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api;

[ApiToken]
[Route("api/charts")]
public sealed class ChartsController : Controller
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public ChartsController(ICurrentUserAccessor currentUser,
        IMediator mediator)
    {
        _currentUser = currentUser;
        _mediator = mediator;
    }

    [HttpGet("random")]
    public async Task<IActionResult> GetRandom(
        [FromQuery(Name = "Count")] [DefaultValue(50)]
        int count = 5,
        [FromQuery(Name = "ChartTypes")] string[]? chartTypeString = null,
        [FromQuery(Name = "SongTypes")] string[]? songTypeString = null,
        [FromQuery(Name = "LevelMin")] int? minInt = null,
        [FromQuery(Name = "LevelMax")] int? maxInt = null)
    {
        var settings = new RandomSettings();

        var chartTypes = chartTypeString == null
            ? new[] { ChartType.Single, ChartType.Double }
            : chartTypeString.Where(s => Enum.TryParse<ChartType>(s, out var _))
                .Select(Enum.Parse<ChartType>).ToArray();


        if (songTypeString != null)
        {
            if (songTypeString.Any(s => !Enum.TryParse<SongType>(s, out var _)))
                return BadRequest($"Invalid Song Type. Options are: {string.Join(',', Enum.GetValues<SongType>())}");
            foreach (var type in songTypeString.Select(Enum.Parse<SongType>)) settings.SongTypeWeights[type] = 1;
        }
        else
        {
            foreach (var type in Enum.GetValues<SongType>()) settings.SongTypeWeights[type] = 1;
        }

        if (count < 1)
            return BadRequest("Invalid count, minimum is 1");

        settings.Count = count;

        if (minInt != null && !DifficultyLevel.IsValid(minInt.Value))
            return BadRequest(
                $"Minimum Difficulty Level  must be between {DifficultyLevel.Min} and {DifficultyLevel.Max}");

        if (maxInt != null && !DifficultyLevel.IsValid(maxInt.Value))
            return BadRequest(
                $"Maximum Difficulty Level  must be between {DifficultyLevel.Min} and {DifficultyLevel.Max}");

        if (minInt > maxInt)
            return BadRequest("Minimum Difficulty must be less than Maximum Difficulty");

        for (var lvl = minInt ?? DifficultyLevel.Min; lvl <= (maxInt ?? DifficultyLevel.Max); lvl++)
        {
            if (chartTypes.Contains(ChartType.Single)) settings.LevelWeights[lvl] = 1;

            if (chartTypes.Contains(ChartType.Double)) settings.DoubleLevelWeights[lvl] = 1;

            if (chartTypes.Contains(ChartType.CoOp) && lvl <= 5) settings.PlayerCountWeights[lvl] = 1;
        }

        try
        {
            var charts = await _mediator.Send(new GetRandomChartsQuery(settings));

            return Json(charts.Select(c => new ChartDto(c)).ToArray());
        }
        catch (RandomizerException e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "Mix")] [DefaultValue("Phoenix")]
        string mixString,
        [FromQuery(Name = "Page")] [DefaultValue(1)]
        int page = 1,
        [FromQuery(Name = "Count")] [DefaultValue(50)]
        int count = 50,
        [FromQuery(Name = "ChartType")] string? chartTypeString = null,
        [FromQuery(Name = "Level")] int? levelInt = null)
    {
        if (!Enum.TryParse<MixEnum>(mixString, out var mix))
            return BadRequest($"Invalid Mix. Options are: {string.Join(',', Enum.GetValues<MixEnum>())}");

        if (!Enum.TryParse<ChartType>(chartTypeString, out var chartType) && chartTypeString != null)
            return BadRequest($"Invalid Chart Type. Options are: {string.Join(',', Enum.GetValues<ChartType>())}");

        if (page < 1)
            return BadRequest("Invalid page, minimum is 1");
        if (count < 1)
            return BadRequest("Invalid count, minimum is 1");

        if (levelInt != null && !DifficultyLevel.IsValid(levelInt.Value))
            return BadRequest($"Difficulty Level must be between {DifficultyLevel.Min} and {DifficultyLevel.Max}");

        var allCharts = (await _mediator.Send(new GetChartsQuery(mix,
            levelInt != null ? DifficultyLevel.From(levelInt.Value) : null,
            chartTypeString == null ? null : chartType))).ToArray();
        var charts = allCharts
            .Skip((page - 1) * count)
            .Take(count)
            .ToArray();
        return Json(new PageDto<ChartDto>
        {
            Count = charts.Length,
            Page = page,
            TotalResults = allCharts.Length,
            Results = charts.Select(c => new ChartDto(c)).ToArray()
        });
    }
}