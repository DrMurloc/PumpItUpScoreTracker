using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     PIU Center's step analysis. Never readable through the API before v2.
///     <para>
///         Scoring difficulty used to live here too and now rides on the chart itself — it keys on
///         (chart, mix), which is exactly what a chart DTO already is, and a separate resource for
///         one float made an integrator join two calls to answer one question.
///     </para>
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
public sealed class ChartAnalysisController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public ChartAnalysisController(IMediator mediator)
    {
        _mediator = mediator;
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
