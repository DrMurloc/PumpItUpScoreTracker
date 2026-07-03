using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using System.ComponentModel;
using System.Security.Authentication;
using CsvHelper.Configuration.Attributes;
using MediatR;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using Chart = ScoreTracker.SharedKernel.Models.Chart;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api;

[ApiToken]
[Route("api/phoenixScores")]
[EnableCors("API")]
public sealed class PhoenixScoresController : Controller
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public PhoenixScoresController(ICurrentUserAccessor currentUser,
        IMediator mediator)
    {
        _currentUser = currentUser;
        _mediator = mediator;
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportScores([FromBody] PhoenixImportRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.Username)) return BadRequest("Missing Username.");

        if (string.IsNullOrWhiteSpace(body.Password)) return BadRequest("Missing Password.");
        try
        {
            var cards = (await _mediator.Send(new GetGameCardsQuery(body.Username, body.Password))).ToArray();
            var gameTag = cards.First();
            if (!string.IsNullOrWhiteSpace(body.GameTag))
            {
                var match = cards.FirstOrDefault(c => c.GameTag == body.GameTag);
                if (match == null) return BadRequest($"GameTag {body.GameTag} couldn't be found for {body.Username}.");

                gameTag = match;
            }

            await _mediator.Send(new ImportOfficialPlayerScoresCommand(body.Username, body.Password, gameTag.Id,
                gameTag.GameTag, body.IncludeBroken, body.SyncScoreTracker));
            return Ok();
        }
        catch (InvalidCredentialException)
        {
            return BadRequest("Username + Password combination was incorrect.");
        }
    }

    [HttpPost]
    public async Task<IActionResult> RecordScore([FromBody] RecordPhoenixScoreDto body,
        [FromQuery(Name = "KeepBestStats")] [Default(false)]
        bool keepBestStats = false)
    {
        if (!Name.TryParse(body.SongName, out var songName)) return BadRequest("Song name is invalid");

        if (!DifficultyLevel.TryParse(body.ChartLevel, out var level)) return BadRequest("Difficulty level is invalid");

        if (!Enum.TryParse<ChartType>(body.ChartType, out var chartType)) return BadRequest("Chart Type is Invalid");

        if (body.Plate != null && !Enum.TryParse<PhoenixPlate>(body.Plate, out _))
            return BadRequest("Plate is Invalid");
        if (body.Score != null && !PhoenixScore.TryParse(body.Score.Value, out _))
            return BadRequest("Score is invalid");

        var chart = await _mediator.Send(new GetChartQuery(MixEnum.Phoenix, songName, level, chartType));
        if (chart == null) return NotFound("Chart not found");

        await _mediator.Send(new UpdatePhoenixBestAttemptCommand(chart.Id, body.IsBroken, body.Score,
            body.Plate == null ? null : Enum.Parse<PhoenixPlate>(body.Plate), keepBestStats));
        return Ok();
    }

    private static readonly string[] SortKeys =
        { "RecordedDate", "Score", "LetterGrade", "Plate", "Level", "Pumbility", "PumbilityPlus" };

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery(Name = "Page")] [DefaultValue(1)] int page,
        [FromQuery(Name = "Count")] [DefaultValue(50)] int count = 50,
        [FromQuery(Name = "SortBy")] [DefaultValue("RecordedDate")] string sortBy = "RecordedDate",
        [FromQuery(Name = "SortDir")] [DefaultValue("Desc")] string sortDir = "Desc",
        [FromQuery(Name = "MinLevel")] int? minLevel = null,
        [FromQuery(Name = "MaxLevel")] int? maxLevel = null,
        [FromQuery(Name = "ChartType")] string? chartType = null,
        [FromQuery(Name = "MinLetterGrade")] string? minLetterGrade = null,
        [FromQuery(Name = "MinPlate")] string? minPlate = null,
        [FromQuery(Name = "IsBroken")] bool? isBroken = null)
    {
        var sortKey = SortKeys.FirstOrDefault(k => k.Equals(sortBy, StringComparison.OrdinalIgnoreCase));
        if (sortKey == null) return BadRequest($"SortBy is invalid, valid values: {string.Join(", ", SortKeys)}");

        if (!sortDir.Equals("Asc", StringComparison.OrdinalIgnoreCase) &&
            !sortDir.Equals("Desc", StringComparison.OrdinalIgnoreCase))
            return BadRequest("SortDir is invalid, valid values: Asc, Desc");
        var descending = sortDir.Equals("Desc", StringComparison.OrdinalIgnoreCase);

        ChartType? typeFilter = null;
        if (chartType != null)
        {
            if (!Enum.TryParse<ChartType>(chartType, true, out var parsedType))
                return BadRequest(
                    $"ChartType is invalid, valid values: {string.Join(", ", Enum.GetNames<ChartType>())}");
            typeFilter = parsedType;
        }

        PhoenixLetterGrade? minGrade = null;
        if (minLetterGrade != null)
        {
            minGrade = Enum.GetValues<PhoenixLetterGrade>().Cast<PhoenixLetterGrade?>()
                .FirstOrDefault(g =>
                    g!.Value.ToString().Equals(minLetterGrade, StringComparison.OrdinalIgnoreCase) ||
                    g!.Value.GetName().Equals(minLetterGrade, StringComparison.OrdinalIgnoreCase));
            if (minGrade == null)
                return BadRequest(
                    $"MinLetterGrade is invalid, valid values: {string.Join(", ", Enum.GetValues<PhoenixLetterGrade>().Select(g => g.GetName()))}");
        }

        PhoenixPlate? minPlateFilter = null;
        if (minPlate != null)
        {
            if (!Enum.TryParse<PhoenixPlate>(minPlate.Replace(" ", ""), true, out var parsedPlate))
                return BadRequest(
                    $"MinPlate is invalid, valid values: {string.Join(", ", Enum.GetValues<PhoenixPlate>().Select(p => p.GetName()))}");
            minPlateFilter = parsedPlate;
        }

        var records = (await _mediator.Send(new GetPhoenixRecordsQuery(_currentUser.User.Id))).ToArray();
        var charts = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix))).ToDictionary(c => c.Id);

        // Mirrors PlayerRatingSaga's per-score stats exactly so the API number matches the site.
        var pumbilityScoring = ScoringConfiguration.PumbilityScoring(true);
        var pumbilityPlusScoring = ScoringConfiguration.PumbilityPlus;
        var rows = records.Where(r => charts.ContainsKey(r.ChartId)).Select(r =>
        {
            var chart = charts[r.ChartId];
            return (Record: r, Chart: chart,
                Pumbility: r.Score == null
                    ? (double?)null
                    : Math.Round(
                        pumbilityScoring.GetScore(chart, r.Score.Value, r.Plate ?? PhoenixPlate.RoughGame,
                            r.IsBroken), 2),
                PumbilityPlus: r.Score == null
                    ? (double?)null
                    : Math.Round(
                        pumbilityPlusScoring.GetScore(chart, r.Score.Value, r.Plate ?? PhoenixPlate.RoughGame,
                            r.IsBroken), 2));
        });

        if (minLevel != null) rows = rows.Where(x => (int)x.Chart.Level >= minLevel.Value);
        if (maxLevel != null) rows = rows.Where(x => (int)x.Chart.Level <= maxLevel.Value);
        if (typeFilter != null) rows = rows.Where(x => x.Chart.Type == typeFilter.Value);
        if (minGrade != null)
            rows = rows.Where(x => x.Record.Score != null && x.Record.Score.Value.LetterGrade >= minGrade.Value);
        if (minPlateFilter != null)
            rows = rows.Where(x => x.Record.Plate != null && x.Record.Plate.Value >= minPlateFilter.Value);
        if (isBroken != null) rows = rows.Where(x => x.Record.IsBroken == isBroken.Value);

        var filtered = rows.ToArray();

        object? Key((RecordedPhoenixScore Record, Chart Chart, double? Pumbility, double? PumbilityPlus) x)
        {
            return sortKey switch
            {
                "Score" => x.Record.Score == null ? null : (int)x.Record.Score.Value,
                "LetterGrade" => x.Record.Score == null ? null : (int)x.Record.Score.Value.LetterGrade,
                "Plate" => x.Record.Plate == null ? null : (int)x.Record.Plate.Value,
                "Level" => (int)x.Chart.Level,
                "Pumbility" => x.Pumbility,
                "PumbilityPlus" => x.PumbilityPlus,
                _ => x.Record.RecordedDate
            };
        }

        // Nulls (scoreless/plateless records) always sort last; ties break newest-first so
        // pagination stays stable within a sort.
        var withNullsLast = filtered.OrderBy(x => Key(x) == null ? 1 : 0);
        var ordered = descending
            ? withNullsLast.ThenByDescending(Key).ThenByDescending(x => x.Record.RecordedDate)
            : withNullsLast.ThenBy(Key).ThenByDescending(x => x.Record.RecordedDate);

        var next = ordered.Skip((page - 1) * count).Take(count).ToArray();

        return Json(new PageDto<PhoenixRecordDto>
        {
            Count = next.Length,
            Page = page,
            TotalResults = filtered.Length,
            Results = next.Select(x => new PhoenixRecordDto
            {
                IsBroken = x.Record.IsBroken,
                LetterGrade = x.Record.Score?.LetterGrade.GetName(),
                Plate = x.Record.Plate?.GetName(),
                RecordedDate = x.Record.RecordedDate,
                Score = x.Record.Score,
                Pumbility = x.Pumbility,
                PumbilityPlus = x.PumbilityPlus,
                Chart = new ChartDto(x.Chart)
            }).ToArray()
        });
    }
}
