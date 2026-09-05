using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     Scores on a chart across players. Its own controller rather than an action on the catalog
///     one because the catalog is share-free — a key reads all of it — and this read is share-gated
///     exactly like <c>/api/v2/players</c>: a tool sees the players who shared with it, a personal
///     token sees its own user, and nobody else's score is ever on the page.
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/charts")]
public sealed class ChartScoresController : ApiV2ControllerBase
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public ChartScoresController(IMediator mediator, ICurrentUserAccessor currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    ///     Every readable player's best on one chart in one mix: the players who shared with the
    ///     calling tool, or the caller alone with a personal token. Passes first, highest score
    ///     first; failed bests follow, highest score first. A player who has not shared is simply
    ///     absent — the page never says who else has played the chart.
    /// </summary>
    /// <param name="chartId">A chart id from <c>/api/v2/charts</c>.</param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="cursor">The opaque cursor from a previous page's <c>next</c> link.</param>
    /// <param name="limit">Rows per page, 1–500. Defaults to 100.</param>
    [HttpGet("{chartId:guid}/scores")]
    [ProducesResponseType(typeof(ChartScorePageDto), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScores([FromRoute] Guid chartId,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var mixFailure))
            return mixFailure!;

        var charts = (await _mediator.Send(new GetChartsQuery(mix))).ToDictionary(c => c.Id);
        if (!charts.TryGetValue(chartId, out var chart))
            return NotFoundProblem("No chart with that id in this mix.");

        var fingerprint = ContinuationToken.FingerprintOf(CredentialKey(_currentUser), mix, chartId, pageSize);
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        var readable = await ReadablePlayerIds(_mediator, _currentUser);
        var rows = (await _mediator.Send(new GetChartRecordsForPlayersQuery(mix, chartId, readable)))
            // Passes above failed bests, then by score, then by player so a page never reshuffles.
            .OrderBy(r => r.Record.IsBroken || r.Record.Score is null ? 1 : 0)
            .ThenByDescending(r => r.Record.Score is null ? -1 : (int)r.Record.Score.Value)
            .ThenBy(r => r.UserId)
            .ToArray();

        var page = rows.Skip(offset).Take(pageSize).ToArray();
        var identities = await PlayerIdentities.Resolve(_mediator, page.Select(r => r.UserId).ToArray());

        // Only the Phoenix mixes have a PUMBILITY formula — asking for one on a legacy mix throws.
        var scoring = mix.UsesLegacyScoring() ? null : ScoringConfiguration.PumbilityScoring(mix, true);
        var data = new List<ChartScoreDto>(page.Length);
        foreach (var row in page)
        {
            if (!identities.TryGetValue(row.UserId, out var identity)) continue;

            var pumbility = scoring is null || row.Record.Score is null
                ? (double?)null
                : Math.Round(scoring.GetScore(chart, row.Record.Score.Value,
                    row.Record.Plate ?? PhoenixPlate.RoughGame, row.Record.IsBroken), 2);
            data.Add(new ChartScoreDto(identity.UserId, identity.Username, identity.GameTag,
                new PlayerScoreDto(row.Record, mix, pumbility)));
        }

        var next = offset + page.Length < rows.Length
            ? ContinuationToken.FromOffset(offset + page.Length, fingerprint)
            : (ContinuationToken?)null;

        return Json(new ChartScorePageDto
        {
            Mix = mix.ToString(),
            ScoringModel = ScoringModelOf(mix),
            Data = data.ToArray(),
            Limit = pageSize,
            Total = rows.Length,
            Next = next is null ? null : NextUrlFor(next.Value)
        });
    }
}
