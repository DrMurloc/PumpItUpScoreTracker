using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Web.Controllers;

/// <summary>
///     The /Charts CSV export — a UI-support endpoint (culture/sitemap family, not under
///     api/*): it accepts the page's own query-string filters plus a column list,
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
        // ColumnsFor drops what the mix cannot carry, so a stale saved setting naming a
        // Phoenix column cannot put an empty column into an XX file.
        var columns = ChartExport.ColumnsFor(mix)
            .Where(c => wanted.Contains(c.Key, StringComparer.OrdinalIgnoreCase))
            .Where(c => !c.RequiresUser || userId != null)
            .ToArray();
        // Bundles ride the same Columns parameter — one key expands to a whole metric family,
        // so they are resolved separately from the single columns.
        var bundles = ChartExport.Bundles
            .Where(b => wanted.Contains(b.Key, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        IReadOnlyList<string> metricNames = Array.Empty<string>();
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, decimal>>? metrics = null;
        if (bundles.Length > 0)
        {
            var catalog = ChartExport.ExportableMetricNames(
                await _mediator.Send(new GetChartMetricNamesQuery(), cancellationToken));
            metricNames = bundles.SelectMany(b => b.Expand(catalog)).Distinct(StringComparer.Ordinal).ToArray();
            metrics = await _mediator.Send(new GetChartMetricsQuery(), cancellationToken);
        }

        if (columns.Length == 0 && metricNames.Count == 0)
            return BadRequest("No exportable columns requested.");


        var page = await _mediator.Send(query, cancellationToken);
        // Only when a column actually asked: the journal read is the one thing here that is
        // not already in hand, and most exports never want it.
        var playCounts = columns.Any(c => c.Scope == ChartExport.Scope.Phoenix2Only) && userId != null
            ? await _mediator.Send(new GetPlayerChartPlayCountsQuery(userId.Value, mix), cancellationToken)
            : null;
        var context = new ChartExport.ExportContext($"{Request.Scheme}://{Request.Host}", playCounts, metrics);
        var csv = ChartExport.Write(page.Results, columns, context, metricNames);

        var scopeSlug = ChartSlugs.MixSlug(mix);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"charts_{scopeSlug}.csv");
    }
}
