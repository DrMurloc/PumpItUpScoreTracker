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

    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>, e.g. "Phoenix2".</param>
    /// <param name="cursor">Opaque. Follow the envelope's <c>next</c> rather than building one.</param>
    /// <param name="limit">Rows per page, 1–500.</param>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var failure)) return failure!;

        var fingerprint = ContinuationToken.FingerprintOf(mix, pageSize);
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        var songs = await _mediator.Send(new GetSongsQuery(mix));
        var rows = songs.Skip(offset).Take(pageSize).Select(s => new SongV2Dto(s)).ToArray();
        var next = offset + rows.Length < songs.Count
            ? ContinuationToken.FromOffset(offset + rows.Length, fingerprint)
            : (ContinuationToken?)null;

        return CatalogJson(Page(rows, pageSize, songs.Count, next));
    }
}
