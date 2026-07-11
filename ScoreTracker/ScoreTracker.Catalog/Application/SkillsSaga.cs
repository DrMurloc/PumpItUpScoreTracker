using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Catalog.Application;

internal sealed class SkillsSaga : IRequestHandler<GetChartSkillsQuery, IEnumerable<ChartSkillsRecord>>,
    IRequestHandler<UpdateChartSkillCommand>,
    IRequestHandler<GetChartStepAnalysisQuery, ChartStepAnalysisRecord?>
{
    private readonly IExternalChartAliasRepository _aliases;
    private readonly IChartRepository _charts;
    private readonly IChartSkillMetricRepository _metrics;

    public SkillsSaga(IChartRepository charts, IChartSkillMetricRepository metrics,
        IExternalChartAliasRepository aliases)
    {
        _charts = charts;
        _metrics = metrics;
        _aliases = aliases;
    }

    public async Task<ChartStepAnalysisRecord?> Handle(GetChartStepAnalysisQuery request,
        CancellationToken cancellationToken)
    {
        var metrics = await _metrics.GetMetrics(new[] { request.ChartId }, PiuCenterMetrics.Source,
            cancellationToken);
        if (metrics.Count == 0) return null;
        var alias = await _aliases.GetAliasForChart(PiuCenterMetrics.Source, request.ChartId, cancellationToken);

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
            alias?.ExternalKey);
    }

    public async Task<IEnumerable<ChartSkillsRecord>> Handle(GetChartSkillsQuery request,
        CancellationToken cancellationToken)
    {
        return await _charts.GetChartSkills(cancellationToken);
    }

    public async Task Handle(UpdateChartSkillCommand request, CancellationToken cancellationToken)
    {
        await _charts.SaveChartSkills(request.Skills, cancellationToken);
    }
}