using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     D29's five tests, run inside Catalog because they are questions about charts. A rest chart is
///     rest relative to its own folder — mix, chart type and level — so the handler reads the whole
///     folder of every chart asked about, builds the distributions once, and answers from them. A
///     folder is at most a couple of hundred charts, so the pass is in memory.
///     <para>
///         The rule never leaves the vertical: consumers get the verdict and the measurements behind
///         it, not the thresholds. That is what stops the Planner and any later reader from drifting
///         apart about the same chart.
///     </para>
/// </summary>
internal sealed class GetRestChartFactsHandler : IRequestHandler<GetRestChartFactsQuery, IReadOnlyList<RestChartFacts>>
{
    private readonly IChartRepository _charts;
    private readonly IChartSkillMetricRepository _metrics;

    public GetRestChartFactsHandler(IChartRepository charts, IChartSkillMetricRepository metrics)
    {
        _charts = charts;
        _metrics = metrics;
    }

    public async Task<IReadOnlyList<RestChartFacts>> Handle(GetRestChartFactsQuery request,
        CancellationToken cancellationToken)
    {
        var wanted = request.ChartIds.ToHashSet();
        if (wanted.Count == 0) return Array.Empty<RestChartFacts>();

        var all = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken)).ToArray();
        var folders = all.Where(c => wanted.Contains(c.Id))
            .Select(c => (c.Type, Level: (int)c.Level))
            .Distinct()
            .ToHashSet();
        if (folders.Count == 0) return Array.Empty<RestChartFacts>();

        // Every chart in every folder touched: the distributions are what the rule reads against.
        var inFolders = all.Where(c => folders.Contains((c.Type, (int)c.Level))).ToArray();
        var metrics = (await _metrics.GetMetrics(inFolders.Select(c => c.Id), PiuCenterMetrics.Source,
                cancellationToken))
            .GroupBy(m => m.ChartId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(m => m.MetricName, m => (double)m.Value,
                StringComparer.Ordinal));

        var measured = inFolders
            .Select(c => (Chart: c, Measures: Raw(c, metrics)))
            .Where(x => x.Measures != null)
            .ToArray();

        var facts = new List<RestChartFacts>();
        foreach (var folder in folders)
        {
            var group = measured.Where(x => x.Chart.Type == folder.Type && (int)x.Chart.Level == folder.Level)
                .ToArray();
            var steps = group.Select(x => x.Measures!.StepsPerSecond).ToArray();
            var holds = group.Select(x => x.Measures!.HoldShare).ToArray();
            var cruxes = group.Select(x => x.Measures!.CruxDensity).ToArray();

            foreach (var (chart, raw) in group.Where(x => wanted.Contains(x.Chart.Id)))
            {
                var measures = raw! with
                {
                    StepsPercentile = RestChartRule.Percentile(steps, raw.StepsPerSecond),
                    HoldPercentile = RestChartRule.Percentile(holds, raw.HoldShare),
                    CruxPercentile = RestChartRule.Percentile(cruxes, raw.CruxDensity)
                };
                facts.Add(Project(chart.Id, measures));
            }
        }

        return facts;
    }

    private static RestChartFacts Project(Guid chartId, RestChartMeasures m) =>
        new(chartId,
            RestChartRule.IsRest(m),
            m.StepsPerSecond,
            m.StepsPercentile,
            m.StepsPercentile <= RestChartRule.MaxStepsPercentile,
            m.HoldShare,
            m.HoldPercentile,
            m.HoldPercentile >= RestChartRule.MinHoldPercentile,
            !m.HasDrillOrAnchorRun,
            m.HardTwistShare,
            m.HardTwistShare <= RestChartRule.MaxHardTwistShare,
            m.CruxDensity,
            m.CruxPercentile,
            m.CruxPercentile <= RestChartRule.MaxCruxPercentile);

    /// <summary>
    ///     The chart's own measurements, before it knows anything about its folder. Null when the
    ///     analysis is missing or cannot be turned into a rate — a chart with no duration or no note
    ///     count has nothing to divide by, and guessing is worse than being absent.
    /// </summary>
    private static RestChartMeasures? Raw(Chart chart,
        IReadOnlyDictionary<Guid, Dictionary<string, double>> metrics)
    {
        if (!metrics.TryGetValue(chart.Id, out var byName)) return null;

        var seconds = chart.Song.Duration.TotalSeconds;
        if (seconds <= 0) return null;
        if (!byName.TryGetValue(PiuCenterMetrics.TapRows, out var taps)) return null;
        if (chart.NoteCount is not { } notes || notes <= 0) return null;

        double Badge(string name) =>
            byName.TryGetValue(PiuCenterMetrics.BadgeFractionPrefix + name, out var v) ? v : 0;

        return new RestChartMeasures(
            taps / seconds,
            0,
            // Hold share is judgements held rather than stepped; a chart with more tap rows than
            // notes is a data mismatch, not a chart with negative holds.
            Math.Max(0, (notes - taps) / notes),
            0,
            Badge("twist_over90") + Badge("twist_far"),
            byName.TryGetValue(PiuCenterMetrics.CruxEnps, out var crux) ? crux : 0,
            0,
            Badge("drill") > 0 || Badge("anchor_run") > 0);
    }
}
