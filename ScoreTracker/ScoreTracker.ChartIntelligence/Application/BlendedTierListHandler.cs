using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
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
///     The tier-list blend, moved out of the page (tier-lists overhaul C2, design doc
///     §6 Tier 3): weighted combination of the stored tier lists with, when
///     personalized, the player's skill estimates and the similar-players aggregation.
///     Similar players reads C1's materialized UserTierListEntry rows — one set-based
///     read replaces the old per-neighbor GetMyRelativeTierListQuery fan-out.
/// </summary>
internal sealed class BlendedTierListHandler : IRequestHandler<GetBlendedTierListQuery, TierListResult>
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

    private readonly IMemoryCache _cache;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly IUserTierListRepository _userTierLists;

    public BlendedTierListHandler(IMediator mediator, IChartRepository charts, IScoreReader scores,
        IPlayerStatsReader playerStats, IUserTierListRepository userTierLists,
        ICurrentUserAccessor currentUser, IMemoryCache cache)
    {
        _mediator = mediator;
        _charts = charts;
        _scores = scores;
        _playerStats = playerStats;
        _userTierLists = userTierLists;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<TierListResult> Handle(GetBlendedTierListQuery request, CancellationToken cancellationToken)
    {
        var lens = request.Lens.ToString();
        if (!LensModifiers.ContainsKey(lens))
            throw new ArgumentOutOfRangeException(nameof(request.Lens), lens, "Unknown tier list lens");

        var userId = request.Personalized ? request.UserId ?? _currentUser.User.Id : (Guid?)null;
        var cacheKey =
            $"{nameof(BlendedTierListHandler)}_{request.Mix}_{lens}_{request.ChartType}_{request.Level}_{userId?.ToString() ?? "community"}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return await Build(request, lens, userId, cancellationToken);
        }) ?? throw new InvalidOperationException("Blended tier list could not be built");
    }

    private async Task<TierListResult> Build(GetBlendedTierListQuery request, string lens, Guid? userId,
        CancellationToken cancellationToken)
    {
        var modifiers = LensModifiers[lens];
        var folderCharts =
            (await _charts.GetCharts(request.Mix, request.Level, request.ChartType,
                cancellationToken: cancellationToken)).ToArray();

        var sources = new Dictionary<string, IReadOnlyDictionary<Guid, SongTierListEntry>>();
        var provisional = false;
        foreach (var sourceName in StoredSources)
        {
            if (!modifiers.TryGetValue(sourceName, out var weight) || weight <= 0) continue;
            var result = await _mediator.Send(new GetTierListWithFallbackQuery(sourceName, request.Mix),
                cancellationToken);
            provisional |= result.IsProvisionalFallback;
            sources[sourceName] = result.Entries
                .GroupBy(e => e.ChartId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        if (userId != null)
        {
            if (modifiers.TryGetValue("Skill", out var skillWeight) && skillWeight > 0)
                sources["Skill"] = (await BuildSkillEntries(request, folderCharts, userId.Value,
                        cancellationToken))
                    .ToDictionary(e => e.ChartId, e => e);
            if (modifiers.TryGetValue("Similar Players", out var similarWeight) && similarWeight > 0)
                sources["Similar Players"] =
                    (await BuildSimilarPlayerEntries(request, folderCharts, userId.Value, cancellationToken))
                    .ToDictionary(e => e.ChartId, e => e);
        }

        var entries = new List<SongTierListEntry>(folderCharts.Length);
        foreach (var chart in folderCharts)
        {
            var weightTotal = 0.0;
            var weightedScore = 0.0;
            foreach (var (sourceName, sourceEntries) in sources)
            {
                if (!sourceEntries.TryGetValue(chart.Id, out var entry) ||
                    entry.Category == TierListCategory.Unrecorded) continue;
                var weight = modifiers[sourceName];
                weightTotal += weight;
                weightedScore += weight * ((int)entry.Category - 3);
            }

            if (weightTotal < .0001)
            {
                entries.Add(new SongTierListEntry("Final", chart.Id, TierListCategory.Unrecorded, 999999));
                continue;
            }

            var final = weightedScore / weightTotal;
            entries.Add(new SongTierListEntry("Final", chart.Id,
                final < -2.5 ? TierListCategory.Overrated :
                final < -1.5 ? TierListCategory.VeryEasy :
                final < -.5 ? TierListCategory.Easy :
                final <= .5 ? TierListCategory.Medium :
                final <= 1.5 ? TierListCategory.Hard :
                final <= 2.5 ? TierListCategory.VeryHard :
                TierListCategory.Underrated, (int)(final * 100.0)));
        }

        return new TierListResult(entries, provisional);
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
    private const double MinSkillEvidence = 2.0; // effective weighted observations per skill
    private const int MinUsableSkills = 3;
    private const double DefaultSkillWeight = 0.5; // top3-only chips and pre-crawl boolean tags
    private const double EstimateOffset = 500_000; // ProcessIntoTierList treats exactly 0 as Unrecorded

    // Proficiency lives in the 900k-1M band (owner): 990,000 = 90%, anything at or
    // under 900,000 = 0%. Deviations pool over this floored scale so sub-900k scores
    // read as zero proficiency instead of dragging skill estimates linearly.
    private const double SkillScoreFloor = 900_000;
    private const double SkillScoreRange = 100_000;

    private static double Proficiency(int score)
    {
        return Math.Clamp(score - SkillScoreFloor, 0, SkillScoreRange) / SkillScoreRange;
    }

    private async Task<IEnumerable<SongTierListEntry>> BuildSkillEntries(GetBlendedTierListQuery request,
        IReadOnlyCollection<Chart> folderCharts, Guid userId, CancellationToken cancellationToken)
    {
        var level = (int)request.Level;
        var bestScores = (await _scores.GetBestScores(request.Mix, userId, cancellationToken))
            .Where(s => s is { Score: not null, IsBroken: false })
            .ToDictionary(s => s.ChartId);

        var scoredWindowCharts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .Where(c => c.Type == request.ChartType && Math.Abs((int)c.Level - level) <= FolderWindow)
            .Where(c => bestScores.ContainsKey(c.Id))
            .ToArray();

        var chips = await _mediator.Send(new GetChartSkillChipsQuery(
            scoredWindowCharts.Select(c => c.Id).Union(folderCharts.Select(c => c.Id)).ToArray()),
            cancellationToken);

        IReadOnlyList<(Skill Skill, double Weight)> WeightsFor(Chart chart)
        {
            return chips.TryGetValue(chart.Id, out var chartChips) && chartChips.Count > 0
                ? chartChips
                    .Select(c => (c.Skill,
                        c.SegmentFraction != null ? (double)c.SegmentFraction.Value : DefaultSkillWeight))
                    .ToArray()
                : chart.Skills.Select(s => (s, DefaultSkillWeight)).ToArray();
        }

        // Normalize before pooling: deviation from YOUR average within each folder,
        // measured on the floored proficiency scale.
        var folderBaselines = scoredWindowCharts
            .GroupBy(c => (int)c.Level)
            .ToDictionary(g => g.Key, g => g.Average(c => Proficiency((int)bestScores[c.Id].Score!.Value)));

        var pooled = new Dictionary<Skill, (double WeightedDeviation, double Evidence)>();
        foreach (var chart in scoredWindowCharts)
        {
            var decay = FolderDecay[Math.Abs((int)chart.Level - level)];
            var deviation = Proficiency((int)bestScores[chart.Id].Score!.Value) - folderBaselines[(int)chart.Level];
            foreach (var (skill, weight) in WeightsFor(chart))
            {
                var observationWeight = decay * weight;
                var current = pooled.TryGetValue(skill, out var sums) ? sums : (0.0, 0.0);
                pooled[skill] = (current.Item1 + observationWeight * deviation,
                    current.Item2 + observationWeight);
            }
        }

        var skillDeviations = pooled
            .Where(kv => kv.Value.Evidence >= MinSkillEvidence)
            .ToDictionary(kv => kv.Key, kv => kv.Value.WeightedDeviation / kv.Value.Evidence);
        if (skillDeviations.Count < MinUsableSkills)
            return folderCharts.Select(c => new SongTierListEntry("Skill", c.Id, TierListCategory.Unrecorded, 0));

        var estimates = new Dictionary<Guid, double>();
        var silent = new List<SongTierListEntry>();
        foreach (var chart in folderCharts)
        {
            var usable = WeightsFor(chart).Where(p => skillDeviations.ContainsKey(p.Skill)).ToArray();
            if (usable.Length == 0)
            {
                silent.Add(new SongTierListEntry("Skill", chart.Id, TierListCategory.Unrecorded, 9999));
                continue;
            }

            estimates[chart.Id] = EstimateOffset +
                                  usable.Sum(p => p.Weight * skillDeviations[p.Skill]) /
                                  usable.Sum(p => p.Weight);
        }

        return TierListProcessor.ProcessIntoTierList("Skill", estimates).Concat(silent);
    }

    // Similar players, re-architected on C1's materialization: neighbors' folder
    // categories come from UserTierListEntry in one read. Neighbors are selected by
    // COMPETITIVE level (breakdown-page workshop, replacing the old ±1 title level):
    // players within ±1.0 competitive level for the requested chart type, each vote
    // scaled by closeness (linear falloff to zero at the window edge) × how much
    // their folder ratings agree with the requesting player's.
    private const double CompetitiveWindow = 1.0;

    private async Task<IEnumerable<SongTierListEntry>> BuildSimilarPlayerEntries(GetBlendedTierListQuery request,
        IReadOnlyCollection<Chart> folderCharts, Guid userId, CancellationToken cancellationToken)
    {
        var myLevel = CompetitiveLevelFor(
            await _playerStats.GetStats(request.Mix, userId, cancellationToken), request.ChartType);
        // Competitive level 1 is the no-data floor (same guard the tier page uses).
        if (myLevel <= 1) return Array.Empty<SongTierListEntry>();

        var neighborIds = (await _playerStats.GetPlayersByCompetitiveRange(request.Mix, request.ChartType,
                myLevel, CompetitiveWindow, cancellationToken)).ToHashSet();
        neighborIds.Remove(userId);
        if (!neighborIds.Any()) return Array.Empty<SongTierListEntry>();

        var closeness = (await _playerStats.GetStats(request.Mix, neighborIds, cancellationToken))
            .ToDictionary(s => s.UserId, s => Math.Max(0.0,
                1.0 - Math.Abs(CompetitiveLevelFor(s, request.ChartType) - myLevel) / CompetitiveWindow));

        var myEntries =
            (await _mediator.Send(new GetMyRelativeTierListQuery(request.ChartType, request.Level, userId,
                request.Mix), cancellationToken))
            .ToDictionary(e => e.ChartId);
        var neighborEntries =
            (await _userTierLists.GetEntriesForCharts(request.Mix, folderCharts.Select(c => c.Id),
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
            chartWeights[entry.ChartId] =
                current + (TierListCategory.Unrecorded - entry.Category) * userTotals[entry.UserId];
        }

        return TierListProcessor.ProcessIntoTierList("Similar Players", chartWeights);
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
