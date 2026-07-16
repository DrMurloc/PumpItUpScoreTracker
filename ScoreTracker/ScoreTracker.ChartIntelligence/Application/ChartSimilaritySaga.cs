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
///     Nothing here SCORES a chart on play data — the formula reads only the crawl. But
///     the reach limiter does read scoring levels, which makes this job order-dependent on
///     ScoringDifficultySaga (owner-accepted 2026-07-15). It buys a gate the folder alone
///     cannot give: scoring level roams from folder level by `sd 0.95`, 19% of charts sit
///     more than a level off, and a ±2-folder gate was admitting pairs six scoring levels
///     apart. That is a projection of ~50k rows per mix, not a sweep of them — the old
///     per-level score read this job shed in R1 is not coming back.
///     Three reads, two of them live. The precalculated graph answers the common case in a
///     PK-prefix seek; filtered and out-of-window searches rebuild one anchor's row against
///     a reduced target list, because filtering the stored top-20 would return nothing (they
///     are the nearest charts overall — any filter worth applying excludes all of them).
///     The live reads cost one badge sweep of the anchor's (mix, type) pool.
/// </summary>
internal sealed class ChartSimilaritySaga : IConsumer<RecalculateChartSimilarityCommand>,
    IRequestHandler<GetSimilarChartsQuery, IReadOnlyList<ChartSimilarityRecord>>,
    IRequestHandler<GetFilteredSimilarChartsQuery, FilteredSimilarChartsRecord>,
    IRequestHandler<GetLeastSimilarChartsQuery, IReadOnlyList<ChartSimilarityRecord>>
{
    private static readonly ChartType[] SimilarityChartTypes = { ChartType.Single, ChartType.Double };

    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IMediator _mediator;
    private readonly IChartScoringLevelRepository _scoringLevels;
    private readonly IChartSimilarityRepository _similarity;

    public ChartSimilaritySaga(IChartRepository charts, IMediator mediator, IChartSimilarityRepository similarity,
        IChartScoringLevelRepository scoringLevels, IDateTimeOffsetAccessor clock)
    {
        _charts = charts;
        _mediator = mediator;
        _similarity = similarity;
        _scoringLevels = scoringLevels;
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
        var scoringLevels = await _scoringLevels.GetScoringLevels(mix, cancellationToken);

        foreach (var chartType in SimilarityChartTypes)
        {
            var typeCharts = charts.Where(c => c.Type == chartType).ToArray();
            if (typeCharts.Length == 0) continue;

            var pool = typeCharts.Select(chart => BuildFeatures(chart, badgeCoverage, stepAnalyses, scoringLevels)).ToArray();
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

    public async Task<FilteredSimilarChartsRecord> Handle(GetFilteredSimilarChartsQuery request,
        CancellationToken cancellationToken)
    {
        var pool = await BuildPoolFor(request.Mix, request.ChartId, cancellationToken);
        if (pool == null) return new FilteredSimilarChartsRecord(Array.Empty<ChartSimilarityRecord>(), 0);
        var (anchor, anchorChart, features, charts) = pool.Value;

        // The level range is the reader's, not the anchor's neighbourhood: unfiltered it
        // defaults to the ±1 the graph would have precalculated, but D18→D23 is the point
        // of asking live.
        var minLevel = request.MinLevel ?? anchorChart.Level - ChartSimilarityCalculator.LevelWindow;
        var maxLevel = request.MaxLevel ?? anchorChart.Level + ChartSimilarityCalculator.LevelWindow;
        var targets = charts
            .Where(c => c.Id != anchorChart.Id)
            .Where(c => c.Level >= minLevel && c.Level <= maxLevel)
            .Where(c => request.MinBpm == null || (c.Song.Bpm != null && c.Song.Bpm.Value.Max >= request.MinBpm))
            .Where(c => request.MaxBpm == null || (c.Song.Bpm != null && c.Song.Bpm.Value.Min <= request.MaxBpm))
            .Where(c => WithinScoringLevel(request, features[c.Id], c))
            .Where(c => WithinNps(request, features[c.Id]))
            .ToArray();

        var edges = ChartSimilarityCalculator.BuildEdgesFor(anchor,
            targets.Select(c => features[c.Id]).ToArray(), features.Values.ToArray());

        // Compared counts what the reader's filter selected, not what survived scoring —
        // it exists to make "1 match" read as a narrow filter rather than a broken feature,
        // and a target we had no evidence for was still looked at.
        return new FilteredSimilarChartsRecord(edges.Select(ToRecord).ToArray(), targets.Length);
    }

    /// <summary>
    ///     An unmeasured chart filters at its listed level, which is what
    ///     GetChartScoringLevelsQuery reports for it everywhere else — so the count the
    ///     reader watched while dragging is the count they get back.
    ///     Deliberately NOT the same reading as
    ///     <see cref="ChartSimilarityCalculator.WithinReach" />, which stays measured-only.
    ///     The gate asks whether we have evidence two charts score alike; a filter asks
    ///     whether a chart is in the range someone pointed at. Feeding the fallback to the
    ///     gate would apply the ±1.25 test to the ~13% that currently escape it on a
    ///     folder-only pass, quietly re-cutting every chart's suggestions.
    /// </summary>
    private static bool WithinScoringLevel(GetFilteredSimilarChartsQuery request, ChartSimilarityFeatures features,
        Chart chart)
    {
        if (request.MinScoringLevel == null && request.MaxScoringLevel == null) return true;
        var scoringLevel = features.ScoringLevel ?? chart.Level;
        return (request.MinScoringLevel == null || scoringLevel >= request.MinScoringLevel)
               && (request.MaxScoringLevel == null || scoringLevel <= request.MaxScoringLevel);
    }

    /// <summary>
    ///     No NPS means no answer. There is no listed value to fall back to the way a
    ///     scoring level has its folder, and admitting the chart anyway would put charts
    ///     of unknown speed inside a speed filter.
    /// </summary>
    private static bool WithinNps(GetFilteredSimilarChartsQuery request, ChartSimilarityFeatures features)
    {
        if (request.MinNps == null && request.MaxNps == null) return true;
        if (features.Nps is not { } nps) return false;
        return (request.MinNps == null || nps >= request.MinNps)
               && (request.MaxNps == null || nps <= request.MaxNps);
    }

    public async Task<IReadOnlyList<ChartSimilarityRecord>> Handle(GetLeastSimilarChartsQuery request,
        CancellationToken cancellationToken)
    {
        var pool = await BuildPoolFor(request.Mix, request.ChartId, cancellationToken);
        if (pool == null) return Array.Empty<ChartSimilarityRecord>();
        var (anchor, anchorChart, features, charts) = pool.Value;

        // The SAME reach the graph uses, both halves — not just the folder. The pairs the
        // scoring-level half throws out are the ones furthest apart in scoreability, which
        // makes them the ones that would win "least similar": gate on the folder alone and
        // the joke fills up with charts the graph refuses to compare at all. "Least like
        // this, of the charts we'd compare it to" is the claim; anything else is a list of
        // things that were never in the conversation.
        var targets = charts
            .Where(c => c.Id != anchorChart.Id)
            .Select(c => features[c.Id])
            .Where(t => ChartSimilarityCalculator.WithinReach(anchor, t))
            .ToArray();

        // BuildEdgesFor ranks best-first; the joke is at the other end, and the worst of
        // them leads — the shelf always opens with the most of whatever it is showing.
        return ChartSimilarityCalculator.BuildEdgesFor(anchor, targets, features.Values.ToArray())
            .Reverse().Take(request.Count).Select(ToRecord).ToArray();
    }

    /// <summary>
    ///     The anchor's whole (mix, chart type) pool, or null when the anchor is missing,
    ///     is a Co-Op chart, or the crawl never covered it. The full pool comes back rather
    ///     than just the targets because intensity z-scores against the folder — a chart is
    ///     unusual relative to its peers, never relative to a filter someone typed.
    /// </summary>
    private async Task<(ChartSimilarityFeatures Anchor, Chart AnchorChart,
        IReadOnlyDictionary<Guid, ChartSimilarityFeatures> Features, IReadOnlyList<Chart> Charts)?> BuildPoolFor(
        MixEnum mix, Guid chartId, CancellationToken cancellationToken)
    {
        var allCharts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).ToArray();
        var anchorChart = allCharts.FirstOrDefault(c => c.Id == chartId);
        if (anchorChart == null || !SimilarityChartTypes.Contains(anchorChart.Type)) return null;

        var charts = allCharts.Where(c => c.Type == anchorChart.Type).ToArray();
        var chartIds = charts.Select(c => c.Id).ToArray();
        var badgeCoverage = await _mediator.Send(new GetChartBadgeCoverageQuery(chartIds), cancellationToken);
        var stepAnalyses = await _mediator.Send(new GetChartStepAnalysesQuery(chartIds), cancellationToken);
        var scoringLevels = await _scoringLevels.GetScoringLevels(mix, cancellationToken);
        var features = charts.ToDictionary(c => c.Id,
            c => BuildFeatures(c, badgeCoverage, stepAnalyses, scoringLevels));
        return (features[anchorChart.Id], anchorChart, features, charts);
    }

    private static ChartSimilarityRecord ToRecord(ChartSimilarityEdge edge)
    {
        return new ChartSimilarityRecord(edge.SimilarChartId, edge.Score, edge.SkillScore, edge.IntensityScore,
            edge.SharedBadges.Select(b => new ChartSharedBadgeRecord(b.Badge, b.Coverage)).ToArray());
    }

    private static ChartSimilarityFeatures BuildFeatures(Chart chart,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, double>> badgeCoverage,
        IReadOnlyDictionary<Guid, ChartStepAnalysisRecord> stepAnalyses,
        IDictionary<Guid, double> scoringLevels)
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
            Fraction(burstSeconds),
            scoringLevels.TryGetValue(chart.Id, out var scoringLevel) ? scoringLevel : null);
    }
}
