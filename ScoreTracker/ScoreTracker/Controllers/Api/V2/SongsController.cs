using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>Songs in a mix, with the artist, duration and BPM range v1 never exposed.</summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/songs")]
public sealed class SongsController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public SongsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The song catalog for one mix, keyed by name, with artist, duration and BPM range.</summary>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>, e.g. "Phoenix2".</param>
    /// <param name="channels">
    ///     Only songs in these channels on this mix, as <c>/api/v2/channels</c> names them. Several:
    ///     a comma list, or repeat the parameter. A song with no known channel never matches.
    /// </param>
    /// <param name="cursor">Opaque. Follow the envelope's <c>next</c> rather than building one.</param>
    /// <param name="limit">Rows per page, 1–500.</param>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPageDto<SongV2Dto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "channel")] string[]? channels = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var failure)) return failure!;

        var (picked, channelProblem) = await ChannelPicks.Resolve(_mediator, mix, channels, (type, title, detail) => Problem(type, title, detail: detail));
        if (channelProblem is not null) return channelProblem;

        var fingerprint = ContinuationToken.FingerprintOf(mix, pageSize, ChannelPicks.Fingerprint(channels));
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        var songs = (await _mediator.Send(new GetSongsQuery(mix)))
            .Where(s => picked is null || (s.Channel is { } channel && picked.Contains(channel)))
            .ToArray();
        var rows = songs.Skip(offset).Take(pageSize).Select(s => new SongV2Dto(s)).ToArray();
        var next = offset + rows.Length < songs.Length
            ? ContinuationToken.FromOffset(offset + rows.Length, fingerprint)
            : (ContinuationToken?)null;

        return CatalogJson(Page(rows, pageSize, songs.Length, next));
    }
}
