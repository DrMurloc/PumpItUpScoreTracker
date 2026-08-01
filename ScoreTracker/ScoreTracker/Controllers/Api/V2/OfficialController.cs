using MediatR;
using Microsoft.AspNetCore.Mvc;
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
[Route(RoutePrefix + "/official")]
public sealed class OfficialController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public OfficialController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <param name="type">Board to read: All, Single, Double, CoOp. Defaults to All.</param>
    [HttpGet("rankings")]
    public async Task<IActionResult> GetRankings(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "type")] string type = "All")
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var rankings = await _mediator.Send(new GetOfficialRankingsQuery(mix, type));
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

    /// <param name="gameTag">The in-game tag as piugame spells it, e.g. "MURLOC#1".</param>
    [HttpGet("players/{gameTag}")]
    public async Task<IActionResult> GetPlayer([FromRoute] string gameTag,
        [FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var profile = await _mediator.Send(new GetOfficialPlayerProfileQuery(mix, gameTag));
        if (profile is null) return NotFoundProblem("No official player with that tag in this mix.");

        return Json(new OfficialPlayerProfileDto(profile));
    }

    [HttpGet("charts/{chartId:guid}/board")]
    public async Task<IActionResult> GetChartBoard([FromRoute] Guid chartId,
        [FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var board = await _mediator.Send(new GetOfficialChartBoardQuery(mix, chartId));
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

    [HttpGet("weekly-highlights")]
    public async Task<IActionResult> GetWeeklyHighlights([FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var highlights = await _mediator.Send(new GetWeeklyHighlightsQuery(mix));
        if (highlights is null) return NotFoundProblem("No weekly snapshot has been sealed for this mix yet.");

        return Json(new WeeklyHighlightsDto(highlights));
    }
}
