using MediatR;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The blend's source computation, extracted from BlendedTierListHandler so the
///     Personalized Breakdown query can expose the same numbers the blend actually
///     uses (breakdown-page workshop) — one implementation, two consumers, no drift.
///     Owns the lens weight tables, the K7 surrounding-folder skill inference, and
///     the competitive-cohort similar-players aggregation.
/// </summary>
internal sealed class TierListBlendBuilder
{
    // Source weights per lens — ported verbatim from the page's modifier table.
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> LensModifiers =
        new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pass"] = new Dictionary<string, double>
                { ["Skill"] = 2, ["Similar Players"] = 1, ["Pass Count"] = 2 },
            ["Score"] = new Dictionary<string, double>
                { ["Official Scores"] = 1, ["Scores"] = 2, ["Skill"] = 2, ["Similar Players"] = 1 },
            ["Popularity"] = new Dictionary<string, double> { ["Popularity"] = 1 },
            ["Chabala"] = new Dictionary<string, double> { ["Chabala"] = 1 },
            ["PG"] = new Dictionary<string, double> { ["PG"] = 1 }
        };

    private static readonly string[] StoredSources =
        { "Official Scores", "Scores", "Popularity", "Pass Count", "PG", "Chabala" };

    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IMediator _mediator;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly IUserTierListRepository _userTierLists;

    public TierListBlendBuilder(IMediator mediator, IChartRepository charts, IScoreReader scores,
        IPlayerStatsReader playerStats, IUserTierListRepository userTierLists, IDateTimeOffsetAccessor clock)
    {
        _mediator = mediator;
        _charts = charts;
        _scores = scores;
        _playerStats = playerStats;
        _userTierLists = userTierLists;
        _clock = clock;
    }

    public static bool IsKnownLens(string lens)
    {
        return LensModifiers.ContainsKey(lens);
    }

    public static IReadOnlyDictionary<string, double> ModifiersFor(string lens)
    {
        return LensModifiers[lens];
    }

    public static bool IsStoredSource(string sourceName)
    {
        return StoredSources.Contains(sourceName);
    }

    public async Task<BlendComputation> Compute(ChartType chartType, DifficultyLevel level, string lens,
        Guid? userId, MixEnum mix, CancellationToken cancellationToken)
    {
        var modifiers = LensModifiers[lens];
        var folderCharts =
            (await _charts.GetCharts(mix, level, chartType, cancellationToken: cancellationToken)).ToArray();

        var sources = new Dictionary<string, IReadOnlyDictionary<Guid, SongTierListEntry>>();
        var provisional = false;
        foreach (var sourceName in StoredSources)
        {
            if (!modifiers.TryGetValue(sourceName, out var weight) || weight <= 0) continue;
            var result = await _mediator.Send(new GetTierListWithFallbackQuery(sourceName, mix),
                cancellationToken);
            provisional |= result.IsProvisionalFallback;
            sources[sourceName] = result.Entries
                .GroupBy(e => e.ChartId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        SkillSourceComputation? skill = null;
        SimilarPlayersComputation? similar = null;
        if (userId != null)
        {
            if (modifiers.TryGetValue("Skill", out var skillWeight) && skillWeight > 0)
            {
                skill = await ComputeSkillSource(chartType, level, mix, userId.Value, folderCharts,
                    cancellationToken);
                sources["Skill"] = skill.Entries;
            }

            if (modifiers.TryGetValue("Similar Players", out var similarWeight) && similarWeight > 0)
            {
                similar = await ComputeSimilarPlayers(chartType, level, mix, userId.Value, folderCharts,
                    cancellationToken);
                sources["Similar Players"] = similar.Entries;
            }
        }

        return new BlendComputation(folderCharts, sources, modifiers, provisional, skill, similar);
    }

    /// <summary>
    ///     The per-chart weighted combine — identical math for the blend's final list
    ///     and the breakdown's community/personalized columns. Sources without an
    ///     entry (or with Unrecorded) simply don't vote; no votes at all = Unrecorded.
    /// </summary>
    public static SongTierListEntry Combine(string listName, Guid chartId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<Guid, SongTierListEntry>> sources,
        IReadOnlyDictionary<string, double> modifiers)
    {
        var weightTotal = 0.0;
        var weightedScore = 0.0;
        foreach (var (sourceName, sourceEntries) in sources)
        {
            if (!modifiers.TryGetValue(sourceName, out var weight) || weight <= 0) continue;
            if (!sourceEntries.TryGetValue(chartId, out var entry) ||
                entry.Category == TierListCategory.Unrecorded) continue;
            weightTotal += weight;
            weightedScore += weight * ((int)entry.Category - 3);
        }

        if (weightTotal < .0001) return new SongTierListEntry(listName, chartId, TierListCategory.Unrecorded, 999999);

        var final = weightedScore / weightTotal;
        return new SongTierListEntry(listName, chartId,
            final < -2.5 ? TierListCategory.Overrated :
            final < -1.5 ? TierListCategory.VeryEasy :
            final < -.5 ? TierListCategory.Easy :
            final <= .5 ? TierListCategory.Medium :
            final <= 1.5 ? TierListCategory.Hard :
            final <= 2.5 ? TierListCategory.VeryHard :
            TierListCategory.Underrated, (int)(final * 100.0));
    }

    // K7 (piucenter follow-up, owner-approved F3): the surrounding-folder skill
    // inference. The old version only saw the viewed folder and went silent under 10
    // scored charts — dead exactly when someone breaks into a new folder. Now every
    // scored chart within ±3 folders votes: per folder the player's scores normalize
    // to deviations from their own folder average (you can't compare raw scores
    // across levels), each (chart, skill) observation is weighted by folder decay ×
    // the skill's segment coverage (a token-drills chart barely votes on Drills), and
    // folder-chart estimates are the coverage-weighted mean of their skills' pooled
    // deviations. Constants are tunable; decay ladder owner-locked 2026-07-11.
    private static readonly double[] FolderDecay = { 1.0, 0.6, 0.3, 0.15 };
    private const int FolderWindow = 3;
    public const double MinSkillEvidence = 2.0; // effective weighted observations per skill
    public const int MinUsableSkills = 3;
    private const double EstimateOffset = 500_000; // ProcessIntoTierList treats exactly 0 as Unrecorded

    // Proficiency lives in the 900k-1M band (owner): 990,000 = 90%, anything at or
    // under 900,000 = 0%. Deviations pool over this floored scale so sub-900k scores
    // read as zero proficiency instead of dragging skill estimates linearly.
    // SkillScoreRange is public: PlayerSkillDeviationsHandler converts pooled
    // deviations to score units with it (proficiency × range).
    private const double SkillScoreFloor = 900_000;
    public const double SkillScoreRange = 100_000;

    private static double Proficiency(int score)
    {
        return Math.Clamp(score - SkillScoreFloor, 0, SkillScoreRange) / SkillScoreRange;
    }

    /// <summary>
    ///     The pooled per-skill evidence around an anchor folder — the reusable core of
    ///     the Skill source, also served cross-vertical through
    ///     GetPlayerSkillDeviationsQuery (Pumbility projections v2). extraChipChartIds
    ///     lets ComputeSkillSource keep its single bulk chips fetch for the
    ///     folder-estimate stage that follows.
    /// </summary>
    public async Task<SkillEvidencePool> ComputeSkillEvidence(ChartType chartType, DifficultyLevel anchorLevel,
        MixEnum mix, Guid userId, IReadOnlyCollection<Guid> extraChipChartIds, CancellationToken cancellationToken)
    {
        var intLevel = (int)anchorLevel;
        var bestScores = (await _scores.GetBestScores(mix, userId, cancellationToken))
            .Where(s => s is { Score: not null, IsBroken: false })
            .ToDictionary(s => s.ChartId);

        var scoredWindowCharts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .Where(c => c.Type == chartType && Math.Abs((int)c.Level - intLevel) <= FolderWindow)
            .Where(c => bestScores.ContainsKey(c.Id))
            .ToArray();

        var chips = await _mediator.Send(new GetChartSkillChipsQuery(
                scoredWindowCharts.Select(c => c.Id).Union(extraChipChartIds).ToArray()),
            cancellationToken);

        // Age outliers over the whole ±3 window: scores much older than the rest of
        // the record vote quietly. Baselines MUST use the same weights — a stale
        // baseline against fresh observations reads as a phantom deviation.
        var ageWeights = ScoreAgePolicy.AgeOutlierWeights(
            scoredWindowCharts.Select(c => (c.Id, bestScores[c.Id].RecordedDate)), _clock.Now);
        var outdatedScoreCount = ageWeights.Count(kv => kv.Value < 1.0);

        // Normalize before pooling: deviation from YOUR average within each folder,
        // measured on the floored proficiency scale.
        var folderBaselines = scoredWindowCharts
            .GroupBy(c => (int)c.Level)
            .ToDictionary(g => g.Key, g =>
                g.Sum(c => ageWeights[c.Id] * Proficiency((int)bestScores[c.Id].Score!.Value)) /
                g.Sum(c => ageWeights[c.Id]));

        var pooled = new Dictionary<Skill, (double WeightedDeviation, double Evidence)>();
        foreach (var chart in scoredWindowCharts)
        {
            var decay = FolderDecay[Math.Abs((int)chart.Level - intLevel)];
            var deviation = Proficiency((int)bestScores[chart.Id].Score!.Value) - folderBaselines[(int)chart.Level];
            foreach (var (skill, weight) in WeightsFor(chips, chart))
            {
                var observationWeight = decay * weight * ageWeights[chart.Id];
                var current = pooled.TryGetValue(skill, out var sums) ? sums : (0.0, 0.0);
                pooled[skill] = (current.Item1 + observationWeight * deviation,
                    current.Item2 + observationWeight);
            }
        }

        var pooledSkills = pooled.ToDictionary(kv => kv.Key, kv => new SkillEvidence(
            kv.Value.Evidence <= 0 ? 0 : kv.Value.WeightedDeviation / kv.Value.Evidence,
            kv.Value.Evidence,
            kv.Value.Evidence >= MinSkillEvidence));

        return new SkillEvidencePool(pooledSkills, chips, scoredWindowCharts.Length, outdatedScoreCount);
    }

    private static IReadOnlyList<(Skill Skill, double Weight)> WeightsFor(
        IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillChipRecord>> chips, Chart chart)
    {
        return chips.TryGetValue(chart.Id, out var chartChips) && chartChips.Count > 0
            ? chartChips.Select(c => (c.Skill, c.Weight)).ToArray()
            : chart.Skills.Select(s => (s, ChartSkillChipRecord.DefaultSegmentWeight)).ToArray();
    }

    private async Task<SkillSourceComputation> ComputeSkillSource(ChartType chartType, DifficultyLevel level,
        MixEnum mix, Guid userId, IReadOnlyCollection<Chart> folderCharts, CancellationToken cancellationToken)
    {
        var evidence = await ComputeSkillEvidence(chartType, level, mix, userId,
            folderCharts.Select(c => c.Id).ToArray(), cancellationToken);

        var skillDeviations = evidence.PooledSkills
            .Where(kv => kv.Value.Usable)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Deviation);
        if (skillDeviations.Count < MinUsableSkills)
            return new SkillSourceComputation(
                folderCharts.ToDictionary(c => c.Id,
                    c => new SongTierListEntry("Skill", c.Id, TierListCategory.Unrecorded, 0)),
                evidence.PooledSkills, false, evidence.ScoredChartCount, evidence.OutdatedScoreCount);

        var estimates = new Dictionary<Guid, double>();
        var silent = new List<SongTierListEntry>();
        foreach (var chart in folderCharts)
        {
            var usable = WeightsFor(evidence.Chips, chart).Where(p => skillDeviations.ContainsKey(p.Skill))
                .ToArray();
            if (usable.Length == 0)
            {
                silent.Add(new SongTierListEntry("Skill", chart.Id, TierListCategory.Unrecorded, 9999));
                continue;
            }

            estimates[chart.Id] = EstimateOffset +
                                  usable.Sum(p => p.Weight * skillDeviations[p.Skill]) /
                                  usable.Sum(p => p.Weight);
        }

        return new SkillSourceComputation(
            TierListProcessor.ProcessIntoTierList("Skill", estimates).Concat(silent)
                .ToDictionary(e => e.ChartId, e => e),
            evidence.PooledSkills, true, evidence.ScoredChartCount, evidence.OutdatedScoreCount);
    }

    // Similar players, re-architected on C1's materialization: neighbors' folder
    // categories come from UserTierListEntry in one read. Neighbors are selected by
    // COMPETITIVE level (breakdown-page workshop, replacing the old ±1 title level):
    // players within ±1.0 competitive level for the requested chart type, each vote
    // scaled by closeness (linear falloff to zero at the window edge) × how much
    // their folder ratings agree with the requesting player's.
    public const double CompetitiveWindow = 1.0;

    private async Task<SimilarPlayersComputation> ComputeSimilarPlayers(ChartType chartType, DifficultyLevel level,
        MixEnum mix, Guid userId, IReadOnlyCollection<Chart> folderCharts, CancellationToken cancellationToken)
    {
        var none = new SimilarPlayersComputation(new Dictionary<Guid, SongTierListEntry>(), 0);
        var myLevel = CompetitiveLevelFor(
            await _playerStats.GetStats(mix, userId, cancellationToken), chartType);
        // Competitive level 1 is the no-data floor (same guard the tier page uses).
        if (myLevel <= 1) return none;

        var neighborIds = (await _playerStats.GetPlayersByCompetitiveRange(mix, chartType,
                myLevel, CompetitiveWindow, cancellationToken)).ToHashSet();
        neighborIds.Remove(userId);
        if (!neighborIds.Any()) return none;

        var closeness = (await _playerStats.GetStats(mix, neighborIds, cancellationToken))
            .ToDictionary(s => s.UserId, s => Math.Max(0.0,
                1.0 - Math.Abs(CompetitiveLevelFor(s, chartType) - myLevel) / CompetitiveWindow));

        var myEntries =
            (await _mediator.Send(new GetMyRelativeTierListQuery(chartType, level, userId, mix),
                cancellationToken))
            .ToDictionary(e => e.ChartId);
        var neighborEntries =
            (await _userTierLists.GetEntriesForCharts(mix, folderCharts.Select(c => c.Id),
                cancellationToken))
            .Where(e => neighborIds.Contains(e.UserId) && e.Category != TierListCategory.Unrecorded)
            .ToArray();

        var userTotals = neighborEntries.GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => closeness.GetValueOrDefault(g.Key) * g.Sum(e =>
                myEntries.TryGetValue(e.ChartId, out var mine) && mine.Category != TierListCategory.Unrecorded
                    ? (int)TierListCategory.Unrecorded - Math.Abs(e.Category - mine.Category)
                    : 0));

        var chartWeights = new Dictionary<Guid, double>();
        foreach (var entry in neighborEntries)
        {
            chartWeights.TryGetValue(entry.ChartId, out var current);
            // Freshness (materialized with the entry, folder-scoped per player):
            // era-mixed entries vote quietly; a quit player's coherent snapshot
            // still votes at full voice.
            chartWeights[entry.ChartId] =
                current + (TierListCategory.Unrecorded - entry.Category) * userTotals[entry.UserId] *
                entry.Freshness;
        }

        return new SimilarPlayersComputation(
            TierListProcessor.ProcessIntoTierList("Similar Players", chartWeights)
                .ToDictionary(e => e.ChartId, e => e),
            neighborIds.Count);
    }

    private static double CompetitiveLevelFor(PlayerStatsRecord stats, ChartType chartType)
    {
        return chartType switch
        {
            ChartType.Single => stats.SinglesCompetitiveLevel,
            ChartType.Double => stats.DoublesCompetitiveLevel,
            _ => stats.CompetitiveLevel
        };
    }
}

/// <summary>Everything one blend run computed, for consumers that need the internals.</summary>
internal sealed record BlendComputation(
    IReadOnlyList<Chart> FolderCharts,
    IReadOnlyDictionary<string, IReadOnlyDictionary<Guid, SongTierListEntry>> Sources,
    IReadOnlyDictionary<string, double> Modifiers,
    bool IsProvisionalFallback,
    SkillSourceComputation? Skill,
    SimilarPlayersComputation? Similar);

/// <summary>Per-skill pooled deviation on the proficiency scale + its effective evidence.</summary>
internal sealed record SkillEvidence(double Deviation, double Evidence, bool Usable);

/// <summary>
///     The pooled evidence around an anchor folder, plus the chips fetched to build it
///     (returned so ComputeSkillSource's folder-estimate stage reuses the one bulk read).
/// </summary>
internal sealed record SkillEvidencePool(
    IReadOnlyDictionary<Skill, SkillEvidence> PooledSkills,
    IReadOnlyDictionary<Guid, IReadOnlyList<ChartSkillChipRecord>> Chips,
    int ScoredChartCount,
    int OutdatedScoreCount);

internal sealed record SkillSourceComputation(
    IReadOnlyDictionary<Guid, SongTierListEntry> Entries,
    IReadOnlyDictionary<Skill, SkillEvidence> PooledSkills,
    bool Active,
    int ScoredChartCount,
    int OutdatedScoreCount);

internal sealed record SimilarPlayersComputation(
    IReadOnlyDictionary<Guid, SongTierListEntry> Entries,
    int NeighborCount);
