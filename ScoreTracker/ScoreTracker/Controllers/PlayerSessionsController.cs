using System.Globalization;
using System.Text;
using CsvHelper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Controllers;

/// <summary>
///     UI-support export for the Sessions page — deliberately NOT part of the pinned
///     api/* contract surface (integrators must not build against it). Public players
///     only; everyone else gets a 404 like the page's redirect.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class PlayerSessionsController : Controller
{
    private readonly IMediator _mediator;

    public PlayerSessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("player/{userId:guid}/scorejournal.csv")]
    public async Task<IActionResult> ExportJournal(Guid userId, [FromQuery] MixEnum mix = MixEnum.Phoenix,
        CancellationToken cancellationToken = default)
    {
        var user = await _mediator.Send(new GetUserByIdQuery(userId), cancellationToken);
        if (user is not { IsPublic: true }) return NotFound();

        var rows = new List<RecentSessionsPage.ScoreEventRecord>();
        var page = 1;
        while (true)
        {
            var result = await _mediator.Send(new GetRecentSessionsQuery(userId, mix, page, 50),
                cancellationToken);
            rows.AddRange(result.Groups.SelectMany(g => g.Rows));
            if (page * 50 >= result.TotalGroups) break;
            page++;
        }

        var chartIds = rows.Select(r => r.ChartId).Distinct().ToArray();
        var charts = chartIds.Any()
            ? (await _mediator.Send(new GetChartsQuery(mix, ChartIds: chartIds), cancellationToken))
            .ToDictionary(c => c.Id)
            : new Dictionary<Guid, SharedKernel.Models.Chart>();

        var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            await csv.WriteRecordsAsync(rows.OrderByDescending(r => r.OccurredAt).Select(r => new
            {
                r.OccurredAt,
                Song = charts.TryGetValue(r.ChartId, out var chart) ? chart.Song.Name.ToString() : string.Empty,
                Difficulty = charts.TryGetValue(r.ChartId, out var c2) ? c2.DifficultyString : string.Empty,
                r.Score,
                r.Plate,
                r.IsBroken,
                Classification = r.Classification.ToString(),
                r.Source,
                r.SessionId
            }), cancellationToken);
        }

        stream.Seek(0, SeekOrigin.Begin);
        return File(stream, "text/csv", $"{user.Name}-scorejournal-{mix}.csv");
    }
}
