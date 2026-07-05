using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Dtos.Api;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api
{
    [ApiToken]
    [Route("api/tierlist")]
    [EnableCors("API")]
    public class TierListsController : Controller
    {
        private readonly IMediator _mediator;

        public TierListsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        private async Task<IActionResult> GetTierList(Name tierListName, string chartTypeString, int levelInt,
            string? mixString)
        {
            if (!Enum.TryParse<ChartType>(chartTypeString, out var chartType))
                return BadRequest($"Invalid Chart Type. Options are: {string.Join(',', Enum.GetValues<ChartType>())}");


            if (!DifficultyLevel.TryParse(levelInt, out var level))
                return BadRequest($"Difficulty Level must be between {DifficultyLevel.Min} and {DifficultyLevel.Max}");

            if (!ApiMixParser.TryParse(mixString, out var mix))
                return BadRequest(ApiMixParser.InvalidMessage);
            var charts = (await _mediator.Send(new GetChartsQuery(mix, level, chartType),
                    HttpContext.RequestAborted))
                .ToDictionary(c => c.Id);
            // The API serves the raw per-mix list. GetTierListQuery bakes in the site's silent
            // Phoenix-1 fallback for empty Phoenix 2 lists — unacceptable here, because integrators'
            // Phoenix2 responses would flip content the day P2 data accumulates. Sending the
            // fallback-aware query and discarding provisional results yields exactly the raw list
            // (empty until Phoenix 2 votes exist) for API stability.
            var tierListResult =
                await _mediator.Send(new GetTierListWithFallbackQuery(tierListName, mix), HttpContext.RequestAborted);
            var tierList = tierListResult.IsProvisionalFallback
                ? Array.Empty<SongTierListEntry>()
                : tierListResult.Entries;
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
            [FromQuery(Name = "Level")] [Required] int levelInt,
            [FromQuery(Name = "Mix")] [DefaultValue("Phoenix")]
            string? mixString = null)
        {
            return await GetTierList("Official Scores", chartTypeString, levelInt, mixString);
        }

        [HttpGet("passcount")]
        public async Task<IActionResult> GetPassCountTierList(
            [FromQuery(Name = "ChartType")] [Required]
            string chartTypeString,
            [FromQuery(Name = "Level")] [Required] int levelInt,
            [FromQuery(Name = "Mix")] [DefaultValue("Phoenix")]
            string? mixString = null)
        {
            return await GetTierList("Pass Count", chartTypeString, levelInt, mixString);
        }

        [HttpGet("popularity")]
        public async Task<IActionResult> GetPopularityTierList(
            [FromQuery(Name = "ChartType")] [Required]
            string chartTypeString,
            [FromQuery(Name = "Level")] [Required] int levelInt,
            [FromQuery(Name = "Mix")] [DefaultValue("Phoenix")]
            string? mixString = null)
        {
            return await GetTierList("Popularity", chartTypeString, levelInt, mixString);
        }

        [HttpGet("scores")]
        public async Task<IActionResult> GetScoresTierList(
            [FromQuery(Name = "ChartType")] [Required]
            string chartTypeString,
            [FromQuery(Name = "Level")] [Required] int levelInt,
            [FromQuery(Name = "Mix")] [DefaultValue("Phoenix")]
            string? mixString = null)
        {
            return await GetTierList("Scores", chartTypeString, levelInt, mixString);
        }
    }
}
