using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     Shared plumbing for every api/v2 controller: RFC 9457 problem responses, the collection
///     envelope, and conditional GETs.
///     <para>
///         Errors carry a stable machine-readable <c>type</c> and never framework or exception text
///         (<c>DiagnosticExposureTests</c>). v1 answered a bad parameter with a bare string, which a
///         client can only match on — and which changes the moment someone improves the wording.
///     </para>
/// </summary>
[EnableCors("API")]
public abstract class ApiV2ControllerBase : Controller
{
    public const string RoutePrefix = "api/v2";

    private const string ProblemBase = "https://piuscores.arroweclip.se/errors/";

    /// <summary>Default page size, and the ceiling a caller can ask for.</summary>
    public const int DefaultLimit = 100;

    public const int MaxLimit = 500;

    protected ObjectResult Problem(string type, string title, int status = StatusCodes.Status400BadRequest,
        string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Type = ProblemBase + type,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = Request.Path.Value
        };
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    /// <summary>404 rather than 403 for a resource the caller may not read — a 403 confirms it exists.</summary>
    protected ObjectResult NotFoundProblem(string detail)
    {
        return Problem("not-found", "Not found.", StatusCodes.Status404NotFound, detail);
    }

    protected ObjectResult MixRequiredProblem()
    {
        return Problem("mix-required", "The mix parameter is required.",
            detail: $"Valid values: {V2MixParser.ValidValues}");
    }

    /// <summary>The envelope's <c>scoringModel</c>: <c>phoenix</c> or <c>legacy</c>.</summary>
    protected static string ScoringModelOf(MixEnum mix)
    {
        return mix.UsesLegacyScoring() ? "legacy" : "phoenix";
    }

    /// <summary>
    ///     The player ids the calling credential may read: the tool's share pool, or the caller
    ///     alone on a personal token. Every player-scoped list on v2 starts from this set.
    /// </summary>
    protected async Task<IReadOnlyList<Guid>> ReadablePlayerIds(IMediator mediator, ICurrentUserAccessor currentUser)
    {
        var toolId = User.ToolId();
        return toolId is null
            ? new[] { currentUser.User.Id }
            : await mediator.Send(new GetToolReadablePlayersQuery(toolId.Value));
    }

    /// <summary>
    ///     What a cursor is bound to on the credential side: the tool, or the person. Two personal
    ///     tokens must never be able to trade cursors, so the person is part of the fingerprint.
    /// </summary>
    protected Guid CredentialKey(ICurrentUserAccessor currentUser)
    {
        return User.ToolId() ?? currentUser.User.Id;
    }

    protected ObjectResult InvalidCursorProblem()
    {
        return Problem("invalid-cursor", "The cursor is not valid for this request.",
            detail: "A cursor belongs to the filters it was issued under. Follow the 'next' link " +
                    "rather than reusing a cursor across different filters.");
    }

    /// <summary>
    ///     Resolves mix and paging in one call because every collection endpoint needs the same three
    ///     checks and an endpoint that skips one fails in a way nobody notices until a tool complains.
    /// </summary>
    protected bool TryReadRequest(string? mixValue, int? limit, out MixEnum mix, out int resolvedLimit,
        out ObjectResult? failure)
    {
        resolvedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        failure = null;
        if (V2MixParser.TryParse(mixValue, out mix)) return true;

        failure = MixRequiredProblem();
        return false;
    }

    /// <summary>
    ///     Builds the envelope and the next link. <paramref name="total" /> stays null where counting
    ///     would cost a second pass over the source.
    /// </summary>
    internal CursorPageDto<T> Page<T>(IReadOnlyList<T> rows, int limit, int? total, ContinuationToken? next)
    {
        return new CursorPageDto<T>
        {
            Data = rows.ToArray(),
            Limit = limit,
            Total = total,
            Next = next is null ? null : NextUrlFor(next.Value)
        };
    }

    /// <summary>
    ///     The absolute next-page URL, preserving every filter the caller sent. Internal rather than
    ///     private because an endpoint whose envelope is not <see cref="CursorPageDto{T}" /> — a score
    ///     page also carries its mix and scoring model — still needs the same link.
    /// </summary>
    internal string NextUrlFor(ContinuationToken token)
    {
        var query = Request.Query
            .Where(q => !string.Equals(q.Key, "cursor", StringComparison.OrdinalIgnoreCase))
            .SelectMany(q => q.Value.Select(v => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(v ?? string.Empty)}"))
            .Append($"cursor={Uri.EscapeDataString(token.Encode())}");
        return $"{Request.Scheme}://{Request.Host}{Request.Path}?{string.Join("&", query)}";
    }

    /// <summary>
    ///     Conditional GET for the catalog, which changes a few times a year and is otherwise re-pulled
    ///     in full by every tool on every run.
    ///     <para>
    ///         The ETag is a hash of the response body rather than a version stamp, because the catalog
    ///         has no version stamp and the cheap substitutes are wrong: a row count plus an id
    ///         aggregate would not change when a chart's level does, which is exactly the edit a tool
    ///         most needs to see. Serializing here rather than handing MVC the object keeps it to one
    ///         pass.
    ///     </para>
    /// </summary>
    protected IActionResult CatalogJson(object payload)
    {
        var json = JsonSerializer.Serialize(payload, WireOptions);
        var etag = $"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..32]}\"";
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=0, must-revalidate";

        if (Request.Headers.IfNoneMatch.Any(v => v == etag))
            return StatusCode(StatusCodes.Status304NotModified);

        return Content(json, "application/json");
    }

    /// <summary>
    ///     MVC's own web defaults, so a body written here is byte-identical to one MVC would have
    ///     serialized. Program.cs does not customize JsonOptions; if it ever does, this must follow.
    /// </summary>
    internal static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);
}
