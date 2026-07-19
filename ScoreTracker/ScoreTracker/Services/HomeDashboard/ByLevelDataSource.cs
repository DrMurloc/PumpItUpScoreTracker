using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     The mix-resolved read seam for the By-Level Breakdown widget (C3). Joins the full
///     chart catalog (so Completion can count over the WHOLE folder) to the player's best
///     attempts, normalizing two record shapes into one <see cref="BreakdownRecord" /> set:
///     Phoenix / Phoenix 2 via <see cref="GetPhoenixRecordsQuery" /> (numeric score + plate),
///     everything XX-and-older via <see cref="GetXXBestChartAttemptsQuery" /> (letter grade +
///     pass only — no 1M-normalized score, no plates). The XX query is already mix-generic,
///     so the widget lights up per mix as scores exist.
/// </summary>
public sealed class ByLevelDataSource
{
    private static readonly BreakdownScales PhoenixScales = new(
        Enum.GetValues<PhoenixLetterGrade>().Select(g => g.GetName()).ToArray(),
        Enum.GetValues<PhoenixPlate>().Select(p => p.GetShorthand()).ToArray());

    private static readonly BreakdownScales LegacyScales = new(
        Enum.GetValues<XXLetterGrade>().Select(g => g.ToString()).ToArray(),
        Array.Empty<string>());

    private readonly ChartCatalogCache _catalog;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IMediator _mediator;

    public ByLevelDataSource(IMediator mediator, ChartCatalogCache catalog, IDateTimeOffsetAccessor clock)
    {
        _mediator = mediator;
        _catalog = catalog;
        _clock = clock;
    }

    public static BreakdownScales ScalesFor(MixEnum mix) =>
        mix.UsesLegacyScoring() ? LegacyScales : PhoenixScales;

    public async Task<(IReadOnlyList<BreakdownRecord> Records, BreakdownScales Scales)> Load(
        Guid userId, MixEnum mix, CancellationToken cancellationToken = default)
    {
        var charts = await _catalog.GetCharts(mix, cancellationToken);
        var now = _clock.Now;
        var records = mix.UsesLegacyScoring()
            ? BuildLegacy(charts,
                await _mediator.Send(new GetXXBestChartAttemptsQuery(userId, mix), cancellationToken), now)
            : BuildPhoenix(charts,
                await _mediator.Send(new GetPhoenixRecordsQuery(userId, mix), cancellationToken), now, mix);
        return (records, ScalesFor(mix));
    }

    private static IReadOnlyList<BreakdownRecord> BuildPhoenix(
        IReadOnlyDictionary<Guid, Chart> charts, IEnumerable<RecordedPhoenixScore> scores, DateTimeOffset now,
        MixEnum mix)
    {
        var byChart = scores.GroupBy(s => s.ChartId).ToDictionary(g => g.Key, g => g.First());
        var records = new List<BreakdownRecord>(charts.Count);
        foreach (var chart in charts.Values)
        {
            if (!byChart.TryGetValue(chart.Id, out var score))
            {
                records.Add(Unplayed(chart));
                continue;
            }

            // Metric values are clear-achievements: a fail contributes only its played flag.
            var passed = !score.IsBroken;
            int? scoreValue = passed && score.Score != null ? (int)score.Score.Value : null;
            int? gradeRank = passed && score.Score != null ? (int)score.Score.Value.LetterGradeFor(mix) : null;
            int? plateRank = passed && score.Plate != null ? (int)score.Plate.Value : null;
            var age = Math.Max(0, (int)(now - score.RecordedDate).TotalDays);
            records.Add(new BreakdownRecord(Normalize(chart.Type), Bucket(chart), true, passed,
                scoreValue, gradeRank, plateRank, age));
        }

        return records;
    }

    private static IReadOnlyList<BreakdownRecord> BuildLegacy(
        IReadOnlyDictionary<Guid, Chart> charts, IEnumerable<BestXXChartAttempt> attempts, DateTimeOffset now)
    {
        var byChart = attempts
            .Where(a => a.BestAttempt != null)
            .GroupBy(a => a.Chart.Id)
            .ToDictionary(g => g.Key, g => g.First().BestAttempt!);
        var records = new List<BreakdownRecord>(charts.Count);
        foreach (var chart in charts.Values)
        {
            if (!byChart.TryGetValue(chart.Id, out var attempt))
            {
                records.Add(Unplayed(chart));
                continue;
            }

            var passed = !attempt.IsBroken;
            int? gradeRank = passed ? (int)attempt.LetterGrade : null;
            var age = Math.Max(0, (int)(now - attempt.RecordedOn).TotalDays);
            // Legacy scoring: no 1M-normalized score, no plates.
            records.Add(new BreakdownRecord(Normalize(chart.Type), Bucket(chart), true, passed,
                null, gradeRank, null, age));
        }

        return records;
    }

    private static BreakdownRecord Unplayed(Chart chart) =>
        new(Normalize(chart.Type), Bucket(chart), false, false, null, null, null);

    // Co-op is bucketed by player count (legacy Routine co-ops carry a real Level too, so
    // Chart.PlayerCount is authoritative, not Level); everything else by difficulty level.
    private static int Bucket(Chart chart) =>
        chart.Type == ChartType.CoOp ? chart.PlayerCount : (int)chart.Level;

    private static ChartType Normalize(ChartType type) => type switch
    {
        ChartType.Single or ChartType.SinglePerformance => ChartType.Single,
        ChartType.CoOp => ChartType.CoOp,
        _ => ChartType.Double // Double, DoublePerformance, HalfDouble
    };
}
