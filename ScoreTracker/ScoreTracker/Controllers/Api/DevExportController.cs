using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api;

/// <summary>
///     Raw table exports for the local dev harness — NOT part of the partner API.
///     <para>
///         **Integrators: do not build against these endpoints.** They serialize physical
///         table rows and change — including breaking changes — whenever the schema does,
///         without notice or versioning. They are hidden from Swagger, are not covered by
///         the wire-shape approval tests, and exist solely so a local development copy of
///         the site can populate itself. The stable surface is the documented `api/*`
///         endpoints.
///     </para>
/// </summary>
[ApiToken]
[ApiController]
[Route("dev/export")]
[EnableCors("API")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class DevExportController : Controller
{
    private readonly IDevDataTransfer _transfer;
    private readonly ICurrentUserAccessor _currentUser;

    public DevExportController(IDevDataTransfer transfer, ICurrentUserAccessor currentUser)
    {
        _transfer = transfer;
        _currentUser = currentUser;
    }

    [HttpGet("tables")]
    public IActionResult GetTables()
    {
        return Ok(_transfer.ReferenceTableKeys);
    }

    [HttpGet("myscores")]
    public async Task<IActionResult> GetMyScores()
    {
        return Ok(await _transfer.ExportUserScores(_currentUser.User.Id, HttpContext.RequestAborted));
    }

    [HttpGet("{tableKey}")]
    public async Task<IActionResult> GetTable([FromRoute] string tableKey)
    {
        if (!_transfer.ReferenceTableKeys.Contains(tableKey)) return NotFound();

        return Ok(await _transfer.ExportReferenceRows(tableKey, HttpContext.RequestAborted));
    }
}
