using MassTransit;
using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The similarity-graph saga (docs/design/chart-similarity.md): the nightly rebuild
///     assembles every chart's evidence — raw badge coverage and step analysis, both
///     Catalog contracts over the same piucenter crawl — runs the pure calculator per
///     chart type, and rewrites each chart's edges wholesale. Singles and Doubles only
///     (Co-Op has no competitive-level semantics; excluded v1). Badges come from
///     GetChartBadgeCoverageQuery, never the display chips: comparison needs what was
///     measured, not what reads well as a tag.
///     Nothing here reads scores. Similarity is a statement about charts, so the rebuild
///     depends on the crawl and not on play data — which is also why it no longer sweeps
///     a folder's scores per level, and why its output only changes when piucenter's does.
/// </summary>
internal sealed class ChartSimilaritySaga : IConsumer<RecalculateChartSimilarityCommand>,
    IRequestHandler<GetSimilarChartsQuery, IReadOnlyList<ChartSimilarityRecord>>
{
    private static readonly ChartType[] SimilarityChartTypes = { ChartType.Single, ChartType.Double };

    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IMediator _mediator;
    private readonly IChartSimilarityRepository _similarity;

    public ChartSimilaritySaga(IChartRepository charts, IMediator mediator, IChartSimilarityRepository similarity,
        IDateTimeOffsetAccessor clock)
    {
        _charts = charts;
        _mediator = mediator;
        _similarity = similarity;
        _clock = clock;
    }

    public async Task Consume(ConsumeContext<RecalculateChartSimilarityCommand> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;

        var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .Where(c => SimilarityChartTypes.Contains(c.Type))
            .ToArray();
        if (charts.Length == 0) return;
        var chartIds = charts.Select(c => c.Id).ToArray();

        var badgeCoverage = await _mediator.Send(new GetChartBadgeCoverageQuery(chartIds), cancellationToken);
        var stepAnalyses = await _mediator.Send(new GetChartStepAnalysesQuery(chartIds), cancellationToken);

        foreach (var chartType in SimilarityChartTypes)
        {
            var typeCharts = charts.Where(c => c.Type == chartType).ToArray();
            if (typeCharts.Length == 0) continue;

            var pool = typeCharts.Select(chart => BuildFeatures(chart, badgeCoverage, stepAnalyses)).ToArray();
            var edges = ChartSimilarityCalculator.BuildEdges(pool);
            var computedAt = _clock.Now;
            foreach (var chart in typeCharts)
                await _similarity.ReplaceEdges(mix, chart.Id,
                    edges.TryGetValue(chart.Id, out var chartEdges)
                        ? chartEdges
                        : Array.Empty<ChartSimilarityEdge>(),
                    computedAt, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ChartSimilarityRecord>> Handle(GetSimilarChartsQuery request,
        CancellationToken cancellationToken)
    {
        var edges = await _similarity.GetEdges(request.Mix, request.ChartId, cancellationToken);
        return edges.Select(ToRecord).ToArray();
    }

    private static ChartSimilarityRecord ToRecord(ChartSimilarityEdge edge)
    {
        return new ChartSimilarityRecord(edge.SimilarChartId, edge.Score, edge.SkillScore, edge.IntensityScore,
            edge.SharedBadges.Select(b => new ChartSharedBadgeRecord(b.Badge, b.Coverage)).ToArray());
    }

    private static ChartSimilarityFeatures BuildFeatures(Chart chart,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, double>> badgeCoverage,
        IReadOnlyDictionary<Guid, ChartStepAnalysisRecord> stepAnalyses)
    {
        var badges = badgeCoverage.TryGetValue(chart.Id, out var banked)
            ? banked
            : new Dictionary<string, double>();

        var analysis = stepAnalyses.GetValueOrDefault(chart.Id);
        var durationSeconds = chart.Song.Duration.TotalSeconds;

        double? Fraction(decimal? seconds)
        {
            return seconds != null && durationSeconds > 0 ? (double)seconds.Value / durationSeconds : null;
        }

        // The spikes are the tension that isn't grind. Sustain is a subset of time under
        // tension — Gargoyle - FULL SONG - D25 sits at the boundary with sustain 362 of
        // 362 — so the remainder is what's left once the sustained runs are accounted for,
        // and it can't go below zero however the crawl rounds.
        var burstSeconds = analysis?.TimeUnderTensionSeconds != null && analysis.SustainTimeSeconds != null
            ? Math.Max(0, analysis.TimeUnderTensionSeconds.Value - analysis.SustainTimeSeconds.Value)
            : (decimal?)null;

        return new ChartSimilarityFeatures(
            chart.Id,
            chart.Song.Name,
            chart.Level,
            badges,
            (double?)analysis?.Nps,
            Fraction(analysis?.SustainTimeSeconds),
            Fraction(burstSeconds));
    }
}
