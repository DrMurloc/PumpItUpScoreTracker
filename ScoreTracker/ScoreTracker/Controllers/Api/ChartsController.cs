using System.ComponentModel;
using MediatR;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api;

[ApiToken]
[Route("api/charts")]
[EnableCors("API")]
public sealed class ChartsController : Controller
{
    /// <summary>The mixes this endpoint's wire contract accepts — legacy MixEnum members are site-internal.</summary>
    private static readonly MixEnum[] ApiCatalogMixes = { MixEnum.XX, MixEnum.Phoenix, MixEnum.Phoenix2 };

    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public ChartsController(ICurrentUserAccessor currentUser,
        IMediator mediator)
    {
        _currentUser = currentUser;
        _mediator = mediator;
    }

    /// <summary>
    /// Returns random charts.
    /// </summary>
    /// <param name="buckets">
    /// Minimum pull count buckets. Examples:
    /// Single:2 - Pulls a minimum of 2 singles charts
    /// 22:4 - Pulls a minimum of 4 22s
    /// S19:8 - Pulls a minimum of 8 S19s
    /// S12,D13,D14:4 - Pulls a minimum of 4 charts in the folders of "S12, D13, or D14"
    /// If the total bucket counts exceeds chart count specified, bucket minimums take precedence (I.E: if count=5, but you specify 4 singles and 3 doubles, 7 charts will be pulled)
    /// Combines with other filtered charts, I.E if chartTypeString is set to "Single", and you specify 22:4, only 4 S22s will be pulled.
    /// </param>
    [HttpGet("random")]
    public async Task<IActionResult> GetRandom(
        [FromQuery(Name = "Count")] [DefaultValue(50)]
        int count = 5,
        [FromQuery(Name = "ChartTypes")] string[]? chartTypeString = null,
        [FromQuery(Name = "SongTypes")] string[]? songTypeString = null,
        [FromQuery(Name = "LevelMin")] int? minInt = null,
        [FromQuery(Name = "LevelMax")] int? maxInt = null,
        [FromQuery(Name = "Bucket")] string[]? buckets = null,
        [FromQuery(Name = "Mix")] [DefaultValue("Phoenix")]
        string? mixString = null)
    {
        if (!ApiMixParser.TryParse(mixString, out var mix))
            return BadRequest(ApiMixParser.InvalidMessage);

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
        foreach (var bucket in buckets ?? [])
        {
            var split = bucket.Split(":");
            if (!int.TryParse(split[1], out var weight) || weight < 1 || split.Length != 2)
                return BadRequest(bucket +
                                  " is an invalid bucket. Examples: 'Single:5', '22:3', 'D23:2', 'S21,S22,D23:4'");

            if (Enum.TryParse<ChartType>(split[0], out var ct))
            {
                settings.ChartTypeMinimums[ct] = weight;
            }
            else if (DifficultyLevel.TryParse(split[0], out var dl))
            {
                settings.LevelMinimums[dl] = weight;
            }
            else
            {
                var typeLevelSplit = split[0].Split(",");
                if (typeLevelSplit.Any(subSplit => !DifficultyLevel.TryParseShortHand(subSplit, out _, out _)))
                    return BadRequest(bucket +
                                      " is an invalid bucket. Examples: 'Single:5', '22:3', 'D23:2', 'S21,S22,D23:4'");
                if (typeLevelSplit.Length == 1)
                    settings.ChartTypeLevelMinimums[typeLevelSplit[0]] = weight;
                else
                    settings.CustomMinimums[split[0]] = weight;
            }
        }

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
            var charts = await _mediator.Send(new GetRandomChartsQuery(settings, mix));

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
        string? mixString = null,
        [FromQuery(Name = "Page")] [DefaultValue(1)]
        int page = 1,
        [FromQuery(Name = "Count")] [DefaultValue(50)]
        int count = 50,
        [FromQuery(Name = "ChartType")] string? chartTypeString = null,
        [FromQuery(Name = "Level")] int? levelInt = null)
    {
        // Grandfathered surface: unlike the ApiMixParser endpoints this one predates the Phoenix 2
        // work and accepts XX (the legacy catalog is real, queryable data). Omission now defaults
        // to Phoenix per the API-wide rule — previously it was a 400, so the change is additive.
        // Explicit allowlist, not Enum.TryParse: the legacy-mix MixEnum members are site-internal
        // until an API story exists (docs/design/legacy-mixes.md) — the wire contract stays put.
        var mix = MixEnum.Phoenix;
        if (mixString != null &&
            (!Enum.TryParse(mixString, true, out mix) || !ApiCatalogMixes.Contains(mix)))
            return BadRequest($"Invalid Mix. Options are: {string.Join(',', ApiCatalogMixes)}");

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