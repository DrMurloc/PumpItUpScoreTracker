using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Controllers.Api;

[Route("api/admin")]
public sealed class AdminController : Controller
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPlayerScoreBatchAccumulator _batches;

    public AdminController(ICurrentUserAccessor currentUser, IPlayerScoreBatchAccumulator batches)
    {
        _currentUser = currentUser;
        _batches = batches;
    }

    [HttpGet("scoreBatches")]
    public IActionResult GetScoreBatches()
    {
        if (!_currentUser.IsLoggedInAsAdmin) return NotFound();

        var entries = _batches.Dump();
        return Json(new
        {
            Count = entries.Count,
            Entries = entries
        });
    }
}
