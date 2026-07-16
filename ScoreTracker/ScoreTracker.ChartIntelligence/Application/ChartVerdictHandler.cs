using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     Assembles the verdict engine's evidence (docs/design/chart-verdicts.md): the
///     vertical's own tier lists, letter percentiles (persisted 0–100, engine wants 0–1),
///     the chart's score population bucketed by competitive level, banked skills/step
///     analysis via Catalog contracts, and the cross-mix level history derived from the
///     mix catalogs. Facets cache per (chart, mix) until shortly after the nightly
///     analytics chain (13:00 UTC) so meta descriptions stay stable between rebuilds.
/// </summary>
internal sealed class ChartVerdictHandler : IRequestHandler<GetChartVerdictQuery, IReadOnlyList<ChartVerdictFacet>>
{
    private const string MixLevelsCacheKey = "ChartVerdict__MixLevels";

    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IMediator _mediator;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly ITierListRepository _tierLists;

    public ChartVerdictHandler(IChartRepository charts, ITierListRepository tierLists, IScoreReader scores,
        IPlayerStatsReader playerStats, IMediator mediator, IMemoryCache cache, IDateTimeOffsetAccessor clock)
    {
        _charts = charts;
        _tierLists = tierLists;
        _scores = scores;
        _playerStats = playerStats;
        _mediator = mediator;
        _cache = cache;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ChartVerdictFacet>> Handle(GetChartVerdictQuery request,
        CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync($"ChartVerdict__{request.Mix}__{request.ChartId}", async entry =>
        {
            // Relative, derived from the clock seam — the cache measures expiry on its
            // own (real) clock, so an absolute stamp from an injected clock would skew.
            entry.AbsoluteExpirationRelativeToNow = UntilNextRecompute();
            var inputs = await BuildInputs(request.ChartId, request.Mix, cancellationToken);
            return ChartVerdictService.ComputeFacets(inputs);
        }))!;
    }

    /// <summary>The analytics chain ends by 12:xx UTC; verdicts refresh just after 13:00.</summary>
    private TimeSpan UntilNextRecompute()
    {
        var now = _clock.Now;
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddHours(13);
        return (now < today ? today : today.AddDays(1)) - now;
    }

    private async Task<ChartVerdictInputs> BuildInputs(Guid chartId, MixEnum mix,
        CancellationToken cancellationToken)
    {
        var chart = await _charts.GetChart(mix, chartId, cancellationToken);

        var passTier = await TierCategory(mix, "Pass Count", chartId, cancellationToken);
        var scoreTier = await TierCategory(mix, "Scores", chartId, cancellationToken);
        var letters = (await _charts.GetChartLetterGradeDifficulties(new[] { chartId }, cancellationToken))
            .FirstOrDefault();
        var letterPercentiles = letters?.Percentiles
            .ToDictionary(kv => kv.Key, kv => kv.Value / 100.0);

        // Just this chart's population — an indexed per-chart read, not a whole-folder scan
        // that kept one chart's rows and threw the rest away.
        var records = (await _scores.GetChartScores(mix, chartId, cancellationToken)).ToArray();
        var scored = records.Where(r => r.Record.Score != null).ToArray();
        var clears = scored.Where(r => !r.Record.IsBroken).ToArray();

        var buckets = (await _playerStats.GetStats(mix, records.Select(r => r.UserId).Distinct(),
                cancellationToken))
            .ToDictionary(s => s.UserId, s => (int)Math.Round(chart.Type == ChartType.Single
                ? s.SinglesCompetitiveLevel
                : s.DoublesCompetitiveLevel));

        var averagesByLevel = scored
            .Where(r => buckets.ContainsKey(r.UserId))
            .GroupBy(r => buckets[r.UserId])
            .Select(g => new LevelAverage(g.Key, g.Average(r => (double)(int)r.Record.Score!.Value)))
            .ToArray();
        var passesByLevel = clears
            .Where(r => buckets.ContainsKey(r.UserId))
            .GroupBy(r => buckets[r.UserId])
            .Select(g => new LevelPasses(g.Key, g.Count()))
            .ToArray();

        var clearScores = clears.Select(r => (int)r.Record.Score!.Value).OrderBy(s => s).ToArray();
        var medianClearScore = clearScores.Length == 0 ? (int?)null : clearScores[(clearScores.Length - 1) / 2];
        var clearPlates = clears.Where(r => r.Record.Plate != null).Select(r => r.Record.Plate!.Value).ToArray();

        var skillWeights = (await _mediator.Send(new GetChartSkillChipsQuery(new[] { chartId }),
                cancellationToken))
            .TryGetValue(chartId, out var chips)
            ? chips.GroupBy(c => c.Skill).ToDictionary(g => g.Key, g => g.Max(c => c.Weight))
            : new Dictionary<Skill, double>();
        var analysis = await _mediator.Send(new GetChartStepAnalysisQuery(chartId), cancellationToken);
        var durationSeconds = chart.Song.Duration.TotalSeconds;
        var tensionFraction = analysis?.TimeUnderTensionSeconds != null && durationSeconds > 0
            ? (double)analysis.TimeUnderTensionSeconds.Value / durationSeconds
            : (double?)null;

        var mixLevels = (await MixLevelMap(cancellationToken)).TryGetValue(chartId, out var levels)
            ? levels
            : Array.Empty<MixLevel>();

        return new ChartVerdictInputs(passTier, scoreTier, letterPercentiles, averagesByLevel, passesByLevel,
            clearPlates, medianClearScore, records.Length, clears.Length, skillWeights, tensionFraction,
            mix, chart.OriginalMix, mixLevels);
    }

    private async Task<TierListCategory?> TierCategory(MixEnum mix, string tierListName, Guid chartId,
        CancellationToken cancellationToken)
    {
        var entry = (await _tierLists.GetAllEntries(mix, tierListName, cancellationToken))
            .FirstOrDefault(e => e.ChartId == chartId);
        return entry?.Category;
    }

    /// <summary>
    ///     Every chart's level in every mix that carries it (shared chart GUIDs across mix
    ///     catalogs — the ChartCompare derivation), era order. One sweep, cached daily:
    ///     the per-chart lookup is what History reads.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MixLevel>>> MixLevelMap(
        CancellationToken cancellationToken)
    {
        return (await _cache.GetOrCreateAsync(MixLevelsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            var map = new Dictionary<Guid, List<MixLevel>>();
            // One flat ChartMix read, not a sweep of all ~30 full catalogs — the History
            // facet only needs (chart, mix, level), and the catalog sweep took 9-15 seconds.
            foreach (var (chartId, mix, level) in await _charts.GetChartMixLevels(cancellationToken))
            {
                if (!map.TryGetValue(chartId, out var levels)) map[chartId] = levels = new List<MixLevel>();
                levels.Add(new MixLevel(mix, level));
            }

            return map.ToDictionary(kv => kv.Key,
                kv => (IReadOnlyList<MixLevel>)kv.Value.OrderBy(l => l.Mix.DisplayOrder()).ToArray());
        }))!;
    }
}
