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
///     assembles every chart's evidence — banked skills and step analysis (Catalog
///     contracts), this vertical's own tier lists / letter percentiles / scoring levels,
///     and score residuals against the population curve (Ledger + Progression read
///     ports) — runs the pure calculator per chart type, and rewrites each chart's
///     edges wholesale. Singles and Doubles only (Co-Op has no competitive-level
///     semantics; excluded v1).
/// </summary>
internal sealed class ChartSimilaritySaga : IConsumer<RecalculateChartSimilarityCommand>,
    IRequestHandler<GetSimilarChartsQuery, IReadOnlyList<ChartSimilarityRecord>>
{
    private static readonly ChartType[] SimilarityChartTypes = { ChartType.Single, ChartType.Double };

    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IMediator _mediator;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly IChartScoringLevelRepository _scoringLevels;
    private readonly IChartSimilarityRepository _similarity;
    private readonly ITierListRepository _tierLists;

    public ChartSimilaritySaga(IChartRepository charts, ITierListRepository tierLists,
        IChartScoringLevelRepository scoringLevels, IScoreReader scores, IPlayerStatsReader playerStats,
        IMediator mediator, IChartSimilarityRepository similarity, IDateTimeOffsetAccessor clock)
    {
        _charts = charts;
        _tierLists = tierLists;
        _scoringLevels = scoringLevels;
        _scores = scores;
        _playerStats = playerStats;
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

        var skillChips = await _mediator.Send(new GetChartSkillChipsQuery(chartIds), cancellationToken);
        var stepAnalyses = await _mediator.Send(new GetChartStepAnalysesQuery(chartIds), cancellationToken);
        var passTiers = await TierCategories(mix, "Pass Count", cancellationToken);
        var scoreTiers = await TierCategories(mix, "Scores", cancellationToken);
        // Letter percentiles persist 0–100 (ScoringDifficultySaga); the calculator wants 0–1.
        var letterPercentiles = (await _charts.GetChartLetterGradeDifficulties(chartIds, cancellationToken))
            .ToDictionary(l => l.ChartId, l => (IReadOnlyDictionary<ParagonLevel, double>)l.Percentiles
                .ToDictionary(kv => kv.Key, kv => kv.Value / 100.0));
        var scoringLevels = await _scoringLevels.GetScoringLevels(mix, cancellationToken);

        foreach (var chartType in SimilarityChartTypes)
        {
            var typeCharts = charts.Where(c => c.Type == chartType).ToArray();
            if (typeCharts.Length == 0) continue;

            var residuals = await BuildResiduals(mix, chartType, typeCharts, cancellationToken);
            var pool = typeCharts.Select(chart => BuildFeatures(chart, skillChips, stepAnalyses, passTiers,
                scoreTiers, letterPercentiles, scoringLevels, residuals)).ToArray();

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
        return edges.Select(e => new ChartSimilarityRecord(e.SimilarChartId, e.Score, e.StyleScore,
            e.BehaviorScore, e.PlayersScore, e.IntensityScore, e.MetaScore, e.SharedScorers)).ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, TierListCategory>> TierCategories(MixEnum mix, string tierListName,
        CancellationToken cancellationToken)
    {
        return (await _tierLists.GetAllEntries(mix, tierListName, cancellationToken))
            .Where(e => e.Category != TierListCategory.Unrecorded)
            .ToDictionary(e => e.ChartId, e => e.Category);
    }

    /// <summary>
    ///     Per chart: each scorer's delta from the chart's population-average score at
    ///     that scorer's competitive-level bucket — the same curve the chart page draws.
    ///     Broken and scoreless records don't participate; neither do scorers without
    ///     stats in the mix (no bucket to norm against).
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, double>>> BuildResiduals(MixEnum mix,
        ChartType chartType, IReadOnlyList<Chart> typeCharts, CancellationToken cancellationToken)
    {
        var chartIds = typeCharts.Select(c => c.Id).ToHashSet();
        var scoresByChart = new Dictionary<Guid, Dictionary<Guid, double>>();
        foreach (var level in typeCharts.Select(c => (int)c.Level).Distinct())
        foreach (var (userId, record) in await _scores.GetScores(mix, chartType, level, cancellationToken))
        {
            if (record.Score == null || record.IsBroken || !chartIds.Contains(record.ChartId)) continue;
            if (!scoresByChart.TryGetValue(record.ChartId, out var users))
                scoresByChart[record.ChartId] = users = new Dictionary<Guid, double>();
            users[userId] = (int)record.Score.Value;
        }

        var userIds = scoresByChart.Values.SelectMany(u => u.Keys).Distinct().ToArray();
        if (userIds.Length == 0) return new Dictionary<Guid, IReadOnlyDictionary<Guid, double>>();
        var buckets = (await _playerStats.GetStats(mix, userIds, cancellationToken))
            .ToDictionary(s => s.UserId, s => (int)Math.Round(chartType == ChartType.Single
                ? s.SinglesCompetitiveLevel
                : s.DoublesCompetitiveLevel));

        var residuals = new Dictionary<Guid, IReadOnlyDictionary<Guid, double>>();
        foreach (var (chartId, users) in scoresByChart)
        {
            var bucketAverages = users
                .Where(kv => buckets.ContainsKey(kv.Key))
                .GroupBy(kv => buckets[kv.Key])
                .ToDictionary(g => g.Key, g => g.Average(kv => kv.Value));
            residuals[chartId] = users
                .Where(kv => buckets.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value - bucketAverages[buckets[kv.Key]]);
        }

        return residuals;
    }

    private static ChartSimilarityFeatures BuildFeatures(Chart chart,
        IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillChipRecord>> skillChips,
        IReadOnlyDictionary<Guid, ChartStepAnalysisRecord> stepAnalyses,
        IReadOnlyDictionary<Guid, TierListCategory> passTiers,
        IReadOnlyDictionary<Guid, TierListCategory> scoreTiers,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<ParagonLevel, double>> letterPercentiles,
        IDictionary<Guid, double> scoringLevels,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, double>> residuals)
    {
        var weights = skillChips.TryGetValue(chart.Id, out var chips)
            ? chips.GroupBy(c => c.Skill)
                .ToDictionary(g => g.Key, g => g.Max(c => c.Weight))
            : new Dictionary<Skill, double>();

        var analysis = stepAnalyses.GetValueOrDefault(chart.Id);
        var durationSeconds = chart.Song.Duration.TotalSeconds;
        double? Fraction(decimal? seconds)
        {
            return seconds != null && durationSeconds > 0 ? (double)seconds.Value / durationSeconds : null;
        }

        return new ChartSimilarityFeatures(
            chart.Id,
            chart.Song.Name,
            chart.Level,
            weights,
            passTiers.TryGetValue(chart.Id, out var pass) ? pass : null,
            scoreTiers.TryGetValue(chart.Id, out var scoreTier) ? scoreTier : null,
            letterPercentiles.GetValueOrDefault(chart.Id),
            scoringLevels.TryGetValue(chart.Id, out var scoringLevel) ? scoringLevel : null,
            (double?)analysis?.Nps,
            Fraction(analysis?.SustainTimeSeconds),
            Fraction(analysis?.TimeUnderTensionSeconds),
            chart.NoteCount,
            chart.StepArtist,
            chart.Song.Type,
            (double?)chart.Song.Bpm?.Average,
            chart.OriginalMix,
            residuals.TryGetValue(chart.Id, out var chartResiduals)
                ? chartResiduals
                : new Dictionary<Guid, double>());
    }
}
