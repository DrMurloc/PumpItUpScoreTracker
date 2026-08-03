using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;

namespace ScoreTracker.Catalog.Application;

/// <summary>
///     Assembles the flat metric bag into per-chart profiles. The four per-skill families are keyed
///     by the same piucenter skill name, so they join on that suffix; a skill appears in the result
///     if any one family mentions it, because a chart can carry a top-3 pick with no coverage row and
///     dropping it would hide the pick.
/// </summary>
internal sealed class GetChartSkillProfilesHandler
    : IRequestHandler<GetChartSkillProfilesQuery, IReadOnlyList<ChartSkillProfile>>
{
    private readonly IChartSkillMetricRepository _metrics;

    public GetChartSkillProfilesHandler(IChartSkillMetricRepository metrics)
    {
        _metrics = metrics;
    }

    public async Task<IReadOnlyList<ChartSkillProfile>> Handle(GetChartSkillProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var byChart = request.ChartIds is null
            ? await _metrics.GetMetricsByChart(PiuCenterMetrics.Source, cancellationToken)
            : (await _metrics.GetMetrics(request.ChartIds, PiuCenterMetrics.Source, cancellationToken))
            .GroupBy(m => m.ChartId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ChartSkillMetric>)g.ToArray());

        return byChart.Select(kv => Assemble(kv.Key, kv.Value)).OrderBy(p => p.ChartId).ToArray();
    }

    private static ChartSkillProfile Assemble(Guid chartId, IReadOnlyList<ChartSkillMetric> metrics)
    {
        var byName = metrics.ToDictionary(m => m.MetricName, m => m.Value, StringComparer.Ordinal);

        double? Scalar(string name)
        {
            return byName.TryGetValue(name, out var value) ? (double)value : null;
        }

        var skillNames = metrics
            .Select(m => SkillSuffix(m.MetricName))
            .Where(s => s is not null)
            .Select(s => s!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        var skills = skillNames.Select(skill => new ChartSkillCoverage(
            skill,
            Scalar(PiuCenterMetrics.BadgeFractionPrefix + skill),
            (int?)Scalar(PiuCenterMetrics.Top3Prefix + skill),
            (int?)Scalar(PiuCenterMetrics.PracticeRankPrefix + skill),
            byName.ContainsKey(PiuCenterMetrics.LastSegmentPrefix + skill))).ToArray();

        var rare = metrics
            .Where(m => m.MetricName.StartsWith(PiuCenterMetrics.RarePrefix, StringComparison.Ordinal))
            .Select(m => new ChartRarePattern(m.MetricName[PiuCenterMetrics.RarePrefix.Length..], (int)m.Value))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToArray();

        return new ChartSkillProfile(
            chartId,
            (int?)Scalar(PiuCenterMetrics.DataVersion),
            Scalar(PiuCenterMetrics.Nps),
            Scalar(PiuCenterMetrics.DifficultyPrediction),
            Scalar(PiuCenterMetrics.SustainTime),
            Scalar(PiuCenterMetrics.TimeUnderTension),
            Scalar(PiuCenterMetrics.LastSegmentIsPeak) is { } peak ? peak != 0 : null,
            skills,
            rare);
    }

    /// <summary>
    ///     The skill name after a per-skill family's prefix, or null for the scalars and the rare
    ///     patterns — rare keys carry a count suffix ("bracket-5") and are not a skill name.
    /// </summary>
    private static string? SkillSuffix(string metricName)
    {
        foreach (var prefix in new[]
                 {
                     PiuCenterMetrics.BadgeFractionPrefix, PiuCenterMetrics.Top3Prefix,
                     PiuCenterMetrics.PracticeRankPrefix, PiuCenterMetrics.LastSegmentPrefix
                 })
            if (metricName.StartsWith(prefix, StringComparison.Ordinal))
                return metricName[prefix.Length..];

        return null;
    }
}
