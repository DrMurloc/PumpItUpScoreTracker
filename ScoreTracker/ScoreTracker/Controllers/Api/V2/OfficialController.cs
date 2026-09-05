using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     The official piugame boards, mirrored. Everything here is public on piugame's own site, so a
///     key is enough and no player consent is involved — unlike <c>/api/v2/players</c>, which is a
///     player's own record and needs a share.
///     <para>
///         Nothing here carries a PIU Scores user id. The mirror holds one internally when an import
///         has linked the accounts; returning it would hand every caller a piugame-tag-to-site-account
///         map for free, private profiles included.
///     </para>
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/official")]
public sealed class OfficialController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public OfficialController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The PUMBILITY ranking as piugame publishes it, or the PIU Scores Supplemented reading of it.</summary>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="type">Board to read: All, Single, Double, CoOp. Defaults to All.</param>
    /// <param name="supplemented">
    ///     The PIU Scores Supplemented switch from the Official Leaderboards section. When true, public
    ///     PIU Scores accounts' verified scores are folded into the ranking: official rows are never
    ///     displaced, places are renumbered, and a row whose player is on the ranking only because
    ///     PIU Scores knows their scores carries <c>player.isSupplemented</c>. Defaults to false, the
    ///     ranking exactly as piugame publishes it.
    /// </param>
    [HttpGet("rankings")]
    public async Task<IActionResult> GetRankings(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "type")] string type = "All",
        [FromQuery(Name = "supplemented")] bool supplemented = false)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var rankings = await _mediator.Send(new GetOfficialRankingsQuery(mix, type, supplemented));
        return Json(new OfficialRankingsDto
        {
            SnapshotAt = rankings.SnapshotAt,
            RatingIsOfficial = rankings.RatingIsOfficial,
            Data = rankings.Rankings.Select(r => new OfficialRankingDto
            {
                Rank = r.Rank,
                PreviousRank = r.PreviousRank,
                Player = new OfficialPlayerDto(r.Player),
                Rating = r.Rating,
                BoardsInTop = r.BoardsInTop,
                PlayerType = r.PlayerType?.ToString()
            }).ToArray()
        });
    }

    /// <summary>
    ///     One player's standing on the boards: tiles, week-by-week history and every placement. A
    ///     tag piugame's boards do not list but PIU Scores knows answers with an empty profile in the
    ///     official reading; ask for <c>supplemented=true</c> to see where that player actually stands.
    /// </summary>
    /// <param name="gameTag">The in-game tag as piugame spells it, e.g. "MURLOC#1".</param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="supplemented">
    ///     The PIU Scores Supplemented switch. When true, the profile includes the player's verified
    ///     PIU Scores bests below each board's official rows, marked <c>isSupplemented</c>, and
    ///     <c>pumbilityIsSupplemented</c> says when the PUMBILITY value is PIU Scores' computed number
    ///     rather than piugame's. A supplemented rank is only meaningful against
    ///     <c>rankings?supplemented=true</c>. Defaults to false.
    /// </param>
    [HttpGet("players/{gameTag}")]
    public async Task<IActionResult> GetPlayer([FromRoute] string gameTag,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "supplemented")] bool supplemented = false)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var profile = await _mediator.Send(new GetOfficialPlayerProfileQuery(mix, gameTag, supplemented));
        if (profile is null) return NotFoundProblem("No official player with that tag in this mix.");

        return Json(new OfficialPlayerProfileDto(profile));
    }

    /// <summary>The chart's full mirrored board at the latest weekly snapshot.</summary>
    /// <param name="chartId">A chart id from <c>/api/v2/charts</c>.</param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="supplemented">
    ///     The PIU Scores Supplemented switch. When true, verified PIU Scores bests of public accounts
    ///     the board does not list are appended below the official rows, each marked
    ///     <c>player.isSupplemented</c>. Defaults to false.
    /// </param>
    [HttpGet("charts/{chartId:guid}/board")]
    public async Task<IActionResult> GetChartBoard([FromRoute] Guid chartId,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "supplemented")] bool supplemented = false)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var board = await _mediator.Send(new GetOfficialChartBoardQuery(mix, chartId, supplemented));
        if (board is null) return NotFoundProblem("No official board is mirrored for that chart.");

        return Json(new OfficialChartBoardDto
        {
            AsOf = board.AsOf,
            Data = board.Entries.Select(e => new OfficialBoardEntryDto
            {
                Place = e.Place, Player = new OfficialPlayerDto(e.Player), Score = e.Score
            }).ToArray()
        });
    }

    /// <param name="trendSnapshots">How many past snapshots of place history to include, 1–52.</param>
    [HttpGet("popularity")]
    public async Task<IActionResult> GetPopularity(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "trendSnapshots")] int trendSnapshots = 8)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var popularity = await _mediator.Send(
            new GetOfficialPopularityQuery(mix, Math.Clamp(trendSnapshots, 1, 52)));

        var rows = popularity.Select(p => new OfficialPopularityDto
        {
            ChartId = p.ChartId,
            Place = p.Place,
            PreviousPlace = p.PreviousPlace,
            RecentPlaces = p.RecentPlaces.ToArray()
        }).ToArray();

        return Json(Page(rows, rows.Length, rows.Length, null));
    }

    /// <summary>The PUMBILITY cutlines per rank, and the uniform grade ladder that clears each.</summary>
    [HttpGet("what-it-takes")]
    public async Task<IActionResult> GetWhatItTakes(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "type")] string type = "All")
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        return Json(new WhatItTakesDto(await _mediator.Send(new GetWhatItTakesQuery(mix, type))));
    }

    /// <summary>This week on the boards: the pulse, movers, gainers, debuts, climbers, world firsts and new #1s.</summary>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="supplemented">
    ///     The PIU Scores Supplemented switch. When true, every week-over-week kind is recomputed over
    ///     the supplemented boards; world firsts and new #1s stay official, as on the site. Defaults
    ///     to false.
    /// </param>
    [HttpGet("weekly-highlights")]
    public async Task<IActionResult> GetWeeklyHighlights([FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "supplemented")] bool supplemented = false)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var highlights = await _mediator.Send(new GetWeeklyHighlightsQuery(mix, supplemented));
        if (highlights is null) return NotFoundProblem("No weekly snapshot has been sealed for this mix yet.");

        return Json(new WeeklyHighlightsDto(highlights));
    }
}
