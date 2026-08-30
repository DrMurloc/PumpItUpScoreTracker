using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Controllers;

/// <summary>
///     The step-chart JSON pair — UI-support endpoints (the /Charts/Export.csv family, never
///     api/*), one per cache life (docs/design/step-chart-failure-map.md D13):
///     <list type="bullet">
///         <item>
///             the <b>payload</b> changes only at ingest, so it answers with an ETag built
///             from the vintage and revalidates for free between uploads;
///         </item>
///         <item>
///             the <b>pins</b> move with every import and carry the viewer's own runs, so they
///             cache privately and briefly. Pins are placed and clustered HERE — the client
///             draws times, it never touches a judgement count (D10/D11).
///         </item>
///     </list>
/// </summary>
public class StepChartController : Controller
{
    /// <summary>Positions are estimates; runs ending this close are one pin (D1).</summary>
    private const decimal ClusterEpsilonSeconds = 1.5m;

    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public StepChartController(IMediator mediator, ICurrentUserAccessor currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("Charts/StepChart/{chartId:guid}")]
    public async Task<IActionResult> Payload(Guid chartId, string? mix, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MixEnum>(mix, true, out var parsedMix)) return NotFound();
        var record = await _mediator.Send(new GetChartStepChartQuery(chartId, parsedMix), cancellationToken);
        if (record == null) return NotFound();

        var etag = $"\"sc-{record.Vintage}-{chartId:N}-{parsedMix}\"";
        Response.Headers.CacheControl = "public, max-age=3600, must-revalidate";
        Response.Headers.ETag = etag;
        if (Request.Headers.IfNoneMatch.Contains(etag)) return StatusCode(StatusCodes.Status304NotModified);

        return Json(new
        {
            vintage = record.Vintage,
            panels = record.Panels,
            aligned = record.BeatsAligned,
            visibility = record.Visibility.ToString(),
            noteCount = record.NoteCount,
            implied = record.ImpliedTotal,
            // Rows as arrays — a boss chart carries thousands and the keys would be most of
            // the wire: [time, panelMask, leftFootMask, quant, beat|null].
            rows = record.Rows.Select(r => new object?[] { r.Time, r.PanelMask, r.LeftFootMask, r.Quant, r.Beat }),
            holds = record.Holds.Select(h => new object[] { h.Panel, h.Start, h.End, h.IsLeftFoot ? 1 : 0 }),
            segments = record.Segments.Select(s => new object?[] { s.Start, s.End, s.Enps }),
            ranges = record.RangesOfInterest.Select(r => new object[] { r.Start, r.End })
        });
    }

    [HttpGet("Charts/StepChart/{chartId:guid}/Breaks")]
    public async Task<IActionResult> Breaks(Guid chartId, string? mix, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MixEnum>(mix, true, out var parsedMix)) return NotFound();
        Response.Headers.CacheControl = "private, max-age=300";

        var record = await _mediator.Send(new GetChartStepChartQuery(chartId, parsedMix), cancellationToken);
        // Pins exist only under a Full verdict (D9); everything else answers the empty rail so
        // the client needs no second vocabulary for "nothing to draw".
        if (record is not { Visibility: StepChartVisibility.Full } || record.NoteCount is not > 0)
            return Json(Empty());

        var viewerId = _currentUser.IsLoggedIn ? _currentUser.User.Id : (Guid?)null;
        var breaks = (await _mediator.Send(new GetChartStageBreaksQuery(chartId, parsedMix, viewerId),
            cancellationToken)).ToArray();
        if (breaks.Length == 0) return Json(Empty());

        var events = record.Rows.Select(r => r.Time)
            .Concat(record.TickTimes)
            .OrderBy(t => t)
            .ToArray();

        var life = new List<decimal>();
        var pass = new List<decimal>();
        var yours = new List<decimal>();
        foreach (var row in breaks)
        {
            var time = BreakPositionSolver.Place(row.Judged, events, record.NoteCount.Value);
            if (time == null) continue;
            (row.IsNonLifebarBreak ? pass : life).Add(time.Value);
            if (row.IsViewer) yours.Add(time.Value);
        }

        var pins = BreakPositionSolver.Cluster(life, ClusterEpsilonSeconds)
            .Select(c => new { t = c.Time, n = c.Count, from = c.From, to = c.To, cause = "life" })
            .Concat(BreakPositionSolver.Cluster(pass, ClusterEpsilonSeconds)
                .Select(c => new { t = c.Time, n = c.Count, from = c.From, to = c.To, cause = "pass" }))
            .OrderBy(p => p.t)
            .ToArray();

        return Json(new
        {
            total = life.Count + pass.Count,
            life = life.Count,
            pass = pass.Count,
            yours = yours.OrderBy(t => t).ToArray(),
            pins
        });
    }

    private static object Empty()
    {
        return new
        {
            total = 0, life = 0, pass = 0, yours = Array.Empty<decimal>(), pins = Array.Empty<object>()
        };
    }
}
