using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     Derived chart analysis: the site's scoring-difficulty numbers, and PIU Center's step analysis.
///     Neither has ever been readable through the API.
/// </summary>
[ApiToken]
public sealed class ChartAnalysisController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public ChartAnalysisController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    ///     How hard each chart is to <i>score</i> on, as a float, which is a different question from
    ///     its listed level. Per mix, because a chart's scoring difficulty moves when its steps do.
    /// </summary>
    [HttpGet(RoutePrefix + "/chart-scoring-levels")]
    public async Task<IActionResult> GetScoringLevels([FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var levels = await _mediator.Send(new GetChartScoringLevelsQuery(mix));
        var rows = levels
            .OrderBy(kv => kv.Key)
            .Select(kv => new ChartScoringLevelDto { ChartId = kv.Key, ScoringLevel = kv.Value })
            .ToArray();

        return CatalogJson(Page(rows, rows.Length, rows.Length, null));
    }

    /// <summary>
    ///     PIU Center's step analysis: NPS, difficulty prediction, sustain, and per-skill coverage.
    ///     <para>
    ///         Takes no <c>mix</c> parameter, and that is not an oversight — the analysis describes the
    ///         steps, which do not change when a chart's listed level does.
    ///     </para>
    /// </summary>
    /// <param name="chartId">Optional. Omit for every analysed chart.</param>
    [HttpGet(RoutePrefix + "/chart-skills")]
    public async Task<IActionResult> GetSkills(
        [FromQuery(Name = "chartId")] Guid? chartId = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        var pageSize = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var fingerprint = ContinuationToken.FingerprintOf(chartId, pageSize);
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        var profiles = await _mediator.Send(new GetChartSkillProfilesQuery(
            chartId is null ? null : new[] { chartId.Value }));

        var rows = profiles.Skip(offset).Take(pageSize).Select(p => new ChartSkillProfileDto(p)).ToArray();
        var next = offset + rows.Length < profiles.Count
            ? ContinuationToken.FromOffset(offset + rows.Length, fingerprint)
            : (ContinuationToken?)null;

        return CatalogJson(Page(rows, pageSize, profiles.Count, next));
    }
}
