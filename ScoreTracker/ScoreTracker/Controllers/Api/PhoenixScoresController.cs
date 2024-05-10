using System.ComponentModel;
using CsvHelper.Configuration.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api;

[ApiToken]
[Route("api/phoenixScores")]
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

    [HttpPost]
    public async Task<IActionResult> RecordScore([FromBody] RecordPhoenixScoreDto body,
        [FromQuery(Name = "OverwriteHigherScores")]
        [Default(false)]
        [Description(
            "When toggled on, will keep your highest score, plate, and will not overwrite a passed chart with broken. Note: This may make your game stats not line up with official game stats.")]
        bool overwriteHigherScores = false)
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
            body.Plate == null ? null : Enum.Parse<PhoenixPlate>(body.Plate), overwriteHigherScores));
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery(Name = "Page")] [DefaultValue(1)] int page,
        [FromQuery(Name = "Count")] [DefaultValue(50)] int count = 50)
    {
        var records = (await _mediator.Send(new GetPhoenixRecordsQuery(_currentUser.User.Id))).ToArray();
        var next = records.OrderByDescending(r => r.RecordedDate).Skip((page - 1) * count).Take(count).ToArray();
        var charts = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix, ChartIds: next.Select(r => r.ChartId))))
            .ToDictionary(c => c.Id);

        return Json(new PageDto<PhoenixRecordDto>
        {
            Count = next.Length,
            Page = page,
            TotalResults = records.Length,
            Results = next.Select(r => new PhoenixRecordDto
            {
                IsBroken = r.IsBroken,
                LetterGrade = r.Score?.LetterGrade.GetName(),
                Plate = r.Plate?.GetName(),
                RecordedDate = r.RecordedDate,
                Score = r.Score,
                Chart = new ChartDto(charts[r.ChartId])
            }).ToArray()
        });
    }
}