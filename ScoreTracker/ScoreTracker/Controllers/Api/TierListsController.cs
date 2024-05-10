using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api
{
    [ApiToken]
    [Route("api/tierlist")]
    public class TierListsController : Controller
    {
        private readonly IMediator _mediator;

        public TierListsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        private async Task<IActionResult> GetTierList(Name tierListName, string chartTypeString, int levelInt)
        {
            if (!Enum.TryParse<ChartType>(chartTypeString, out var chartType))
                return BadRequest($"Invalid Chart Type. Options are: {string.Join(',', Enum.GetValues<ChartType>())}");


            if (!DifficultyLevel.TryParse(levelInt, out var level))
                return BadRequest($"Difficulty Level must be between {DifficultyLevel.Min} and {DifficultyLevel.Max}");
            var charts = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix, level, chartType),
                    HttpContext.RequestAborted))
                .ToDictionary(c => c.Id);
            var tierList = await _mediator.Send(new GetTierListQuery(tierListName), HttpContext.RequestAborted);
            return Ok(tierList.Where(t => charts.ContainsKey(t.ChartId)).Select(c => new TierListEntryDto
            {
                Category = c.Category.ToString(),
                Order = c.Order,
                Chart = new ChartDto(charts[c.ChartId])
            }));
        }

        [HttpGet("officialscores")]
        public async Task<IActionResult> GetOfficialScoresTierList(
            [FromQuery(Name = "ChartType")] [Required]
            string chartTypeString,
            [FromQuery(Name = "Level")] [Required] int levelInt)
        {
            return await GetTierList("Official Scores", chartTypeString, levelInt);
        }

        [HttpGet("passcount")]
        public async Task<IActionResult> GetPassCountTierList(
            [FromQuery(Name = "ChartType")] [Required]
            string chartTypeString,
            [FromQuery(Name = "Level")] [Required] int levelInt)
        {
            return await GetTierList("Pass Count", chartTypeString, levelInt);
        }

        [HttpGet("popularity")]
        public async Task<IActionResult> GetPopularityTierList(
            [FromQuery(Name = "ChartType")] [Required]
            string chartTypeString,
            [FromQuery(Name = "Level")] [Required] int levelInt)
        {
            return await GetTierList("Popularity", chartTypeString, levelInt);
        }

        [HttpGet("scores")]
        public async Task<IActionResult> GetScoresTierList(
            [FromQuery(Name = "ChartType")] [Required]
            string chartTypeString,
            [FromQuery(Name = "Level")] [Required] int levelInt)
        {
            return await GetTierList("Scores", chartTypeString, levelInt);
        }
    }
}
