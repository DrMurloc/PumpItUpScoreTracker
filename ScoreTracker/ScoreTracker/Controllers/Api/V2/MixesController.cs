using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     Every playable mix. Call this first: <c>scoringModel</c> tells you whether a mix's scores are
///     1M-scale numbers or letter grades, and reading a legacy record as a Phoenix one gives a
///     plausible, wrong answer.
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/mixes")]
public sealed class MixesController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public MixesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>All thirty mixes, oldest first. Takes no mix parameter — it is the mix list.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPageDto<MixDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> Get()
    {
        var mixes = await _mediator.Send(new GetMixesQuery());
        var rows = mixes.Select(m => new MixDto(m)).ToArray();
        return CatalogJson(Page(rows, rows.Length, rows.Length, null));
    }
}
