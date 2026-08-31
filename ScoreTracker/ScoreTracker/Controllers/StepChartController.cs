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
    // Walk-offs are first-class on the stored cause now (StageBreakCauseSolver.WalkOffMissFloor,
    // pass-command-detection D36); this endpoint only reads the flag and un-walks the pin.

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

        // The ETag is the BANK WRITE, not the vintage: a re-upload keeps its vintage, so a
        // vintage-keyed tag would 304 stale payloads for an hour after every re-ingest — the
        // exact window between a deploy and the owner's combined-zip upload (field-hit
        // 2026-08-30). The sc2 prefix marks the wire shape.
        var etag = $"\"sc2-{record.Vintage}-{record.BankedAt.UtcTicks}-{chartId:N}-{parsedMix}\"";
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
            meter = record.Meter,
            // Rows as arrays — a boss chart carries thousands and the keys would be most of
            // the wire: [time, panelMask, leftFootMask, quant, beat|null].
            rows = record.Rows.Select(r => new object?[] { r.Time, r.PanelMask, r.LeftFootMask, r.Quant, r.Beat }),
            holds = record.Holds.Select(h => new object[] { h.Panel, h.Start, h.End, h.IsLeftFoot ? 1 : 0 }),
            // [start, end, enps|null, level|null, badge display names] — badges ship as the
            // hero's display forms (BadgeLabels; English in every locale by standing ruling),
            // so the cached-public JSON never varies by locale and the module needs no table.
            segments = record.Segments.Select(s => new object?[]
            {
                s.Start, s.End, s.Enps, s.Level,
                (s.Badges ?? Array.Empty<string>()).Select(BadgeLabels.DisplayName)
            }),
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
        var result = await _mediator.Send(new GetChartStageBreaksQuery(chartId, parsedMix, viewerId),
            cancellationToken);
        var breaks = result.Breaks;
        if (breaks.Count == 0 && result.Unplaced == 0 && result.FinishedFails == 0) return Json(Empty());

        var events = record.Rows.Select(r => r.Time)
            .Concat(record.TickTimes)
            .OrderBy(t => t)
            .ToArray();

        var life = new List<decimal>();
        var walk = new List<decimal>();
        var pass = new List<(decimal Time, string? Plate, string? Grade)>();
        var yours = new List<decimal>();
        foreach (var row in breaks)
        {
            // A walk-off's judgement count includes the guard's miss tail; the pin belongs at
            // the GIVE-UP point, not where the corpse stopped — subtract the guaranteed
            // consecutive tail before placing (D18).
            var walkedOff = row.IsWalkOff;
            var judged = walkedOff
                ? Math.Max(1, row.Judged - StageBreakCauseSolver.WalkOffMissFloor)
                : row.Judged;
            var time = BreakPositionSolver.Place(judged, events, record.NoteCount.Value);
            if (time == null) continue;
            if (row.IsNonLifebarBreak) pass.Add((time.Value, row.PassPlate, row.PassGrade));
            else if (walkedOff) walk.Add(time.Value);
            else life.Add(time.Value);
            if (row.IsViewer) yours.Add(time.Value);
        }

        // A pass pin carries the game's own command art where the solver named targets — the
        // badge is the sentence, exactly the session page's choice (pass-command-detection D33;
        // wordiness ruling 2026-08-30). Codes are the art file stems PassCommandBadge uses.
        var pins = BreakPositionSolver.Cluster(life)
            .Select(c => new
            {
                t = c.Time, n = c.Count, from = c.From, to = c.To, cause = "life",
                cmds = Array.Empty<string>()
            })
            .Concat(BreakPositionSolver.Cluster(walk)
                .Select(c => new
                {
                    t = c.Time, n = c.Count, from = c.From, to = c.To, cause = "walk",
                    cmds = Array.Empty<string>()
                }))
            .Concat(BreakPositionSolver.Cluster(pass.Select(p => p.Time))
                .Select(c => new
                {
                    t = c.Time, n = c.Count, from = c.From, to = c.To, cause = "pass",
                    cmds = pass.Where(p => p.Time >= c.From && p.Time <= c.To)
                        .SelectMany(p => CommandArt(p.Plate, p.Grade))
                        .Distinct()
                        .Take(3)
                        .ToArray()
                }))
            .OrderBy(p => p.t)
            .ToArray();

        return Json(new
        {
            total = life.Count + walk.Count + pass.Count,
            life = life.Count,
            walk = walk.Count,
            pass = pass.Count,
            unplaced = result.Unplaced,
            finished = result.FinishedFails,
            yours = yours.OrderBy(t => t).ToArray(),
            pins
        });
    }

    /// <summary>
    ///     The command-window art stems for a break's named targets, both when both were named
    ///     (D31). Stored as full names; the art files are keyed the way the badge keys them —
    ///     plate shorthand, grade name.
    /// </summary>
    private static IEnumerable<string> CommandArt(string? plate, string? grade)
    {
        var parsedPlate = PhoenixPlateHelperMethods.TryParse(plate);
        if (parsedPlate != null) yield return $"Pass_Plate_{parsedPlate.Value.GetShorthand()}";
        var parsedGrade = PhoenixLetterGradeHelperMethods.TryParse(grade);
        if (parsedGrade != null) yield return $"Pass_Grade_{parsedGrade.Value.GetName()}";
    }

    private static object Empty()
    {
        return new
        {
            total = 0, life = 0, walk = 0, pass = 0, unplaced = 0, finished = 0,
            yours = Array.Empty<decimal>(), pins = Array.Empty<object>()
        };
    }
}
