using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     A mix's patches. Every <c>released*Version</c> parameter on the chart reads takes one of
///     these names, and <c>chartCount</c> says what each patch added
///     (docs/design/chart-versions.md §3).
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/versions")]
public sealed class VersionsController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public VersionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    ///     The mix's patches, oldest first: the name the game prints on its update notice, the day
    ///     it shipped in Korea (null on a legacy patch nobody dated), its place in the release order,
    ///     and how many charts first appeared in it. A mix nobody has versioned answers with an
    ///     empty list.
    /// </summary>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPageDto<MixVersionDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var versions = await _mediator.Send(new GetMixVersionsQuery(mix));
        var rows = versions.Select(v => new MixVersionDto(v)).ToArray();
        return CatalogJson(Page(rows, rows.Length, rows.Length, null));
    }
}
