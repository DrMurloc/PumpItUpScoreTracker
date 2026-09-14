using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     The channels a mix offers — the folders its song select sorts songs into. Every
///     <c>channel</c> parameter on the chart and song reads takes one of these names
///     (docs/design/song-channels.md §4).
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/channels")]
public sealed class ChannelsController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public ChannelsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    ///     The mix's channels in the game's order: the token the <c>channel</c> parameter takes, the
    ///     name the site prints, its place in the order, and how many songs and charts of the mix
    ///     sit in it. A mix's set is what its songs say — Phoenix lists five, Phoenix 2 four, having
    ///     folded J-Music into World Music — and a mix with no channel data answers with an empty list.
    /// </summary>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPageDto<MixChannelDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var channels = await _mediator.Send(new GetMixChannelsQuery(mix));
        var rows = channels.Select(c => new MixChannelDto(c)).ToArray();
        return CatalogJson(Page(rows, rows.Length, rows.Length, null));
    }
}
