using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.EventCompetition.Application;

/// <summary>
///     The D20 re-rating split. Each season freezes its own chart balance AND its own scoring
///     tables, so a raw cross-season delta mixes "I got better" with "the game changed"; this
///     re-prices the same charts and scores under the target board's frozen configuration and
///     isolates the two moves — the snapshot swapped alone (chart re-ratings) and the tables
///     swapped alone (the re-cut) — each against the stored original. The effects multiply,
///     so the deltas deliberately do not sum to the total. Per-chart pricing follows
///     TournamentSession.Add exactly: each chart floors to int before the sum.
/// </summary>
internal sealed class MoMRepriceHandler : IRequestHandler<RepriceMoMSessionQuery, MoMSessionReprice?>
{
    private readonly IChartRepository _charts;
    private readonly IMoMRepository _mom;

    public MoMRepriceHandler(IMoMRepository mom, IChartRepository charts)
    {
        _mom = mom;
        _charts = charts;
    }

    public async Task<MoMSessionReprice?> Handle(RepriceMoMSessionQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _mom.GetSession(request.SessionId, cancellationToken);
        // Drafts never compare — the compare UI offers published sessions only, and
        // answering for a draft would leak its existence to a stranger.
        if (session?.PublishedAt == null) return null;

        var boards = await _mom.GetBoards(cancellationToken);
        var source = boards.FirstOrDefault(b => b.Id == session.BoardId);
        var target = boards.FirstOrDefault(b => b.Id == request.UnderBoardId);
        if (source == null || target == null) return null;
        // Nothing in MoM ever compares across chart types or mixes (D15).
        if (source.Mix != target.Mix || source.ChartType != target.ChartType) return null;

        var rows = await _mom.GetSessionCharts(session.Id, cancellationToken);
        if (rows.Count == 0) return null;

        var charts = (await _charts.GetCharts(source.Mix,
                chartIds: rows.Select(r => r.ChartId).Distinct().ToArray(),
                cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var sourceTables = await _mom.GetBoardConfiguration(session.BoardId, false,
            cancellationToken);
        var targetTables = await _mom.GetBoardConfiguration(request.UnderBoardId, false,
            cancellationToken);
        if (sourceTables == null || targetTables == null) return null;
        var sourceSnapshot = await _mom.GetSeasonSnapshot(session.BoardId, cancellationToken);
        var targetSnapshot = await _mom.GetSeasonSnapshot(request.UnderBoardId, cancellationToken);

        var snapshotSwapped = Price(sourceTables, targetSnapshot, rows, charts);
        var tablesSwapped = Price(targetTables, sourceSnapshot, rows, charts);
        var repriced = Price(targetTables, targetSnapshot, rows, charts);

        var rerated = rows.Select(r => r.ChartId).Distinct().Count(chartId =>
            Balanced(sourceSnapshot, chartId, charts) != Balanced(targetSnapshot, chartId, charts));

        return new MoMSessionReprice(session.TotalScore, repriced.Total, rerated,
            snapshotSwapped.Total - session.TotalScore, tablesSwapped.Total - session.TotalScore,
            repriced.PerChart);
    }

    private static (int Total, IReadOnlyDictionary<Guid, int> PerChart) Price(
        TournamentConfiguration configuration, IReadOnlyDictionary<Guid, double> snapshot,
        IReadOnlyList<MoMSessionChartRecord> rows, IReadOnlyDictionary<Guid, Chart> charts)
    {
        // Each GetBoardConfiguration call returns a fresh instance, so swapping the snapshot
        // in mutates nothing shared.
        configuration.Scoring.ChartLevelSnapshot = new Dictionary<Guid, double>(snapshot);
        var perChart = new Dictionary<Guid, int>();
        var total = 0;
        foreach (var row in rows)
        {
            if (!charts.TryGetValue(row.ChartId, out var chart)) continue;
            var points = (int)configuration.Scoring.GetScore(chart, row.Score,
                Enum.Parse<PhoenixPlate>(row.Plate), row.IsBroken);
            perChart[row.ChartId] = points;
            total += points;
        }

        return (total, perChart);
    }

    private static double Balanced(IReadOnlyDictionary<Guid, double> snapshot, Guid chartId,
        IReadOnlyDictionary<Guid, Chart> charts)
    {
        if (snapshot.TryGetValue(chartId, out var balanced)) return balanced;
        return charts.TryGetValue(chartId, out var chart) ? (int)chart.Level + 0.5 : 0;
    }
}
