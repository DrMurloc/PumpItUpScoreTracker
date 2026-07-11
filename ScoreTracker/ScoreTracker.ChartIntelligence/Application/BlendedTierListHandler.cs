using MediatR;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IScoreReader _scores;
    private readonly ITierListRepository _tierLists;
    private readonly ITitleRepository _titles;
    private readonly IUserTierListRepository _userTierLists;

    public BlendedTierListHandler(IMediator mediator, IChartRepository charts, IScoreReader scores,
        ITitleRepository titles, ITierListRepository tierLists, IUserTierListRepository userTierLists,
        ICurrentUserAccessor currentUser, IMemoryCache cache)
    {
        _mediator = mediator;
        _charts = charts;
        _scores = scores;
        _titles = titles;
        _tierLists = tierLists;
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
                sources["Skill"] = (await BuildSkillEntries(request.Mix, folderCharts, userId.Value,
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

    // The player's skill-derived difficulty estimates — ported from the page's
    // BuildSkillTierList: average unbroken score per skill in the folder, chart
    // estimate = average of its skills' averages, with the page's original guards
    // (under 10 scored charts or under 3 skills with data → nothing to say).
    private async Task<IEnumerable<SongTierListEntry>> BuildSkillEntries(MixEnum mix,
        IReadOnlyCollection<Chart> folderCharts, Guid userId, CancellationToken cancellationToken)
    {
        if (!folderCharts.Any(c => c.Skills.Any()))
            return folderCharts.Select(c => new SongTierListEntry("Skill", c.Id, TierListCategory.Unrecorded, 0));

        var bestScores = (await _scores.GetBestScores(mix, userId, cancellationToken))
            .Where(s => s is { Score: not null, IsBroken: false })
            .ToDictionary(s => s.ChartId);

        var skillAverages = folderCharts
            .SelectMany(c => c.Skills.Select(s => (Chart: c, Skill: s)))
            .GroupBy(p => p.Skill)
            .ToDictionary(g => g.Key, g =>
            {
                var scored = g.Where(p => bestScores.ContainsKey(p.Chart.Id)).ToArray();
                return scored.Any() ? scored.Average(p => (int)bestScores[p.Chart.Id].Score!.Value) : 0.0;
            });

        var scoredInFolder = folderCharts.Count(c => bestScores.ContainsKey(c.Id));
        if (scoredInFolder < 10 || skillAverages.Count(kv => kv.Value > 0) < 3)
            return folderCharts.Select(c => new SongTierListEntry("Skill", c.Id, TierListCategory.Unrecorded, 0));

        var estimates = folderCharts
            .Where(c => c.Skills.Any(s => skillAverages[s] > 0))
            .ToDictionary(c => c.Id, c => c.Skills.Where(s => skillAverages[s] > 0).Average(s => skillAverages[s]));

        return TierListProcessor.ProcessIntoTierList("Skill", estimates)
            .Concat(folderCharts.Where(c => !c.Skills.Any(s => skillAverages[s] > 0))
                .Select(c => new SongTierListEntry("Skill", c.Id, TierListCategory.Unrecorded, 9999)));
    }

    // Similar players, re-architected on C1's materialization: neighbors' folder
    // categories come from UserTierListEntry in one read; the similarity weighting math
    // is ported verbatim from the page's GetSimilarPlayers.
    private async Task<IEnumerable<SongTierListEntry>> BuildSimilarPlayerEntries(GetBlendedTierListQuery request,
        IReadOnlyCollection<Chart> folderCharts, Guid userId, CancellationToken cancellationToken)
    {
        var myLevel = await _titles.GetCurrentTitleLevel(request.Mix, userId, cancellationToken);
        var neighborIds = new HashSet<Guid>();
        foreach (var level in NeighboringLevels(myLevel))
            neighborIds.UnionWith(await _tierLists.GetUsersOnLevel(request.Mix, level, cancellationToken));
        neighborIds.Remove(userId);
        if (!neighborIds.Any()) return Array.Empty<SongTierListEntry>();

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
            .ToDictionary(g => g.Key, g => g.Sum(e =>
                myEntries.TryGetValue(e.ChartId, out var mine) && mine.Category != TierListCategory.Unrecorded
                    ? (int)TierListCategory.Unrecorded - Math.Abs(e.Category - mine.Category)
                    : 0));

        var chartWeights = new Dictionary<Guid, int>();
        foreach (var entry in neighborEntries)
        {
            chartWeights.TryGetValue(entry.ChartId, out var current);
            chartWeights[entry.ChartId] =
                current + (TierListCategory.Unrecorded - entry.Category) * userTotals[entry.UserId];
        }

        return TierListProcessor.ProcessIntoTierList("Similar Players", chartWeights);
    }

    private static IEnumerable<DifficultyLevel> NeighboringLevels(DifficultyLevel myLevel)
    {
        // Integer math on purpose: myLevel - 1 as a DifficultyLevel would throw for a
        // level-1 player before the range check could reject it.
        int mine = myLevel;
        for (var level = mine - 1; level <= mine + 1; level++)
            if (level >= DifficultyLevel.Min && level <= DifficultyLevel.Max)
                yield return DifficultyLevel.From(level);
    }
}
