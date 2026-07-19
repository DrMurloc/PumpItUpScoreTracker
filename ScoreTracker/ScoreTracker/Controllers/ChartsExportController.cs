using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Web.Controllers;

/// <summary>
///     The /Charts CSV export — a UI-support endpoint (culture/sitemap family, not under
///     api/*): it accepts the page's own query-string filters plus columns and shape,
///     runs the unpaged search, and streams the file. My* columns require the signed-in
///     caller; anonymous requests get them silently dropped.
/// </summary>
public class ChartsExportController : Controller
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly IUiSettingsAccessor _uiSettings;

    public ChartsExportController(IMediator mediator, ICurrentUserAccessor currentUser,
        IUiSettingsAccessor uiSettings)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _uiSettings = uiSettings;
    }

    [HttpGet("Charts/Export.csv")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var mix = Enum.TryParse<MixEnum>(Request.Query["Mix"], true, out var explicitMix)
            ? explicitMix
            : await _uiSettings.GetSelectedMix(cancellationToken);
        var userId = _currentUser.IsLoggedIn ? _currentUser.User.Id : (Guid?)null;
        var query = ChartSearchUrlParser.Parse(Request.Query, mix, userId);

        var requested = Request.Query["Columns"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wanted = requested.Length == 0 ? ChartExport.DefaultColumns : requested;
        var columns = ChartExport.Columns
            .Where(c => wanted.Contains(c.Key, StringComparer.OrdinalIgnoreCase))
            .Where(c => !c.RequiresUser || userId != null)
            .ToArray();
        if (columns.Length == 0) return BadRequest("No exportable columns requested.");

        var shape = Enum.TryParse<ChartExportShape>(Request.Query["Shape"], true, out var parsedShape)
            ? parsedShape
            : ChartExportShape.Grouped;

        var page = await _mediator.Send(query, cancellationToken);
        var csv = ChartExport.Write(page.Results, columns, shape);

        var scopeSlug = query.AllMixes ? "all-mixes" : ChartSlugs.MixSlug(mix);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"charts_{scopeSlug}.csv");
    }
}
