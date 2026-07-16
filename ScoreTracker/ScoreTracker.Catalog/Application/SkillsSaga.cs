using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Application;

internal sealed class SkillsSaga : IRequestHandler<GetChartSkillsQuery, IEnumerable<ChartSkillsRecord>>,
    IRequestHandler<GetChartStepAnalysisQuery, ChartStepAnalysisRecord?>,
    IRequestHandler<GetChartStepAnalysesQuery, IReadOnlyDictionary<Guid, ChartStepAnalysisRecord>>,
    IRequestHandler<GetChartSkillChipsQuery, IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>>,
    IRequestHandler<GetChartBadgeCoverageQuery, IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, double>>>,
    IRequestHandler<GetUnresolvedAliasesQuery, IReadOnlyList<UnresolvedAliasRecord>>,
    IRequestHandler<ResolveExternalAliasCommand>
{
    private readonly IExternalChartAliasRepository _aliases;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IChartSkillMetricRepository _metrics;

    public SkillsSaga(IChartRepository charts, IChartSkillMetricRepository metrics,
        IExternalChartAliasRepository aliases, IDateTimeOffsetAccessor clock)
    {
        _charts = charts;
        _metrics = metrics;
        _aliases = aliases;
        _clock = clock;
    }

    public async Task<IReadOnlyList<UnresolvedAliasRecord>> Handle(GetUnresolvedAliasesQuery request,
        CancellationToken cancellationToken)
    {
        return (await _aliases.GetAliases(request.Source, cancellationToken))
            .Where(a => a.ChartId == null)
            .OrderBy(a => a.ExternalKey)
            .Select(a => new UnresolvedAliasRecord(a.ExternalKey, a.LastCheckedAt))
            .ToArray();
    }

    public async Task Handle(ResolveExternalAliasCommand request, CancellationToken cancellationToken)
    {
        await _aliases.ResolveAlias(request.Source, request.ExternalKey, request.ChartId, _clock.Now,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, double>>> Handle(
        GetChartBadgeCoverageQuery request, CancellationToken cancellationToken)
    {
        var metrics = await _metrics.GetMetrics(request.ChartIds, PiuCenterMetrics.Source, cancellationToken);
        return metrics
            .Where(m => m.MetricName.StartsWith(PiuCenterMetrics.BadgeFractionPrefix, StringComparison.Ordinal))
            .GroupBy(m => m.ChartId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, double>)g.ToDictionary(
                m => m.MetricName[PiuCenterMetrics.BadgeFractionPrefix.Length..],
                m => (double)m.Value));
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>> Handle(
        GetChartSkillChipsQuery request, CancellationToken cancellationToken)
    {
        var metrics = await _metrics.GetMetrics(request.ChartIds, PiuCenterMetrics.Source, cancellationToken);
        var result = new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>();
        foreach (var group in metrics.GroupBy(m => m.ChartId))
        {
            // Best segment coverage per mapped skill (for display), the skills whose
            // coverage cleared their per-skill bar (for membership), and dominance
            // rank from the top-3 pick — the same policy the flip mapper applies.
            var fractions = new Dictionary<Skill, decimal>();
            var qualified = new HashSet<Skill>();
            var topRank = new Dictionary<Skill, decimal>();
            foreach (var metric in group)
                if (metric.MetricName.StartsWith(PiuCenterMetrics.BadgeFractionPrefix, StringComparison.Ordinal))
                {
                    var theirs = metric.MetricName[PiuCenterMetrics.BadgeFractionPrefix.Length..];
                    foreach (var skill in PiuCenterSkillMapper.MapTheirSkill(theirs))
                    {
                        if (!fractions.TryGetValue(skill, out var best) || metric.Value > best)
                            fractions[skill] = metric.Value;
                        if (metric.Value >= PiuCenterSkillMapper.ThresholdFor(theirs)) qualified.Add(skill);
                    }
                }
                else if (metric.MetricName.StartsWith(PiuCenterMetrics.Top3Prefix, StringComparison.Ordinal))
                {
                    foreach (var skill in PiuCenterSkillMapper.MapTheirSkill(
                                 metric.MetricName[PiuCenterMetrics.Top3Prefix.Length..]))
                        if (!topRank.TryGetValue(skill, out var best) || metric.Value < best)
                            topRank[skill] = metric.Value;
                }

            var chips = topRank
                .OrderBy(kv => kv.Value)
                .Select(kv => new ChartSkillChipRecord(kv.Key, true,
                    fractions.TryGetValue(kv.Key, out var f) ? f : null))
                .Concat(qualified
                    .Where(s => !topRank.ContainsKey(s))
                    .OrderByDescending(s => fractions[s])
                    .Select(s => new ChartSkillChipRecord(s, false, fractions[s])))
                .ToArray();
            if (chips.Length > 0) result[group.Key] = chips;
        }

        return result;
    }

    public async Task<ChartStepAnalysisRecord?> Handle(GetChartStepAnalysisQuery request,
        CancellationToken cancellationToken)
    {
        var metrics = await _metrics.GetMetrics(new[] { request.ChartId }, PiuCenterMetrics.Source,
            cancellationToken);
        if (metrics.Count == 0) return null;
        var alias = await _aliases.GetAliasForChart(PiuCenterMetrics.Source, request.ChartId, cancellationToken);
        return ToAnalysisRecord(metrics, alias?.ExternalKey);
    }

    public async Task<IReadOnlyDictionary<Guid, ChartStepAnalysisRecord>> Handle(GetChartStepAnalysesQuery request,
        CancellationToken cancellationToken)
    {
        // One metrics read for the whole sweep, no alias lookups — bulk consumers (the
        // similarity job) read scalars, never link out (the query doc pins ExternalKey null).
        var metrics = await _metrics.GetMetrics(request.ChartIds, PiuCenterMetrics.Source, cancellationToken);
        return metrics.GroupBy(m => m.ChartId)
            .ToDictionary(g => g.Key, g => ToAnalysisRecord(g.ToArray(), externalKey: null));
    }

    private static ChartStepAnalysisRecord ToAnalysisRecord(IReadOnlyList<ChartSkillMetric> metrics,
        string? externalKey)
    {
        decimal? Single(string name)
        {
            return metrics.FirstOrDefault(m => m.MetricName == name)?.Value;
        }

        return new ChartStepAnalysisRecord(
            metrics.Where(m => m.MetricName.StartsWith(PiuCenterMetrics.Top3Prefix, StringComparison.Ordinal))
                .OrderBy(m => m.Value)
                .Select(m => m.MetricName[PiuCenterMetrics.Top3Prefix.Length..])
                .ToArray(),
            metrics.Where(m =>
                    m.MetricName.StartsWith(PiuCenterMetrics.BadgeFractionPrefix, StringComparison.Ordinal))
                .ToDictionary(m => m.MetricName[PiuCenterMetrics.BadgeFractionPrefix.Length..], m => m.Value),
            Single(PiuCenterMetrics.Nps),
            Single(PiuCenterMetrics.SustainTime),
            Single(PiuCenterMetrics.TimeUnderTension),
            Single(PiuCenterMetrics.DifficultyPrediction),
            externalKey);
    }

    public async Task<IEnumerable<ChartSkillsRecord>> Handle(GetChartSkillsQuery request,
        CancellationToken cancellationToken)
    {
        return await _charts.GetChartSkills(cancellationToken);
    }
}