using MassTransit;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     Rebuilds the PUMBILITY census (docs/design/pumbility-tier-list.md): for every folder and
///     every cohort, how many of that cohort's top-50 pools hold each chart.
///     <para>
///         Pools are built once per chart type from a level-by-level bulk read rather than
///         per-player — the alternative is roughly three thousand round trips for a job that
///         only ever wants every pool at once.
///     </para>
/// </summary>
internal sealed class PumbilityCensusSaga : IConsumer<ProcessPumbilityCensusCommand>
{
    /// <summary>The list name the blend reads this source under.</summary>
    internal const string ListName = "PUMBILITY";

    /// <summary>
    ///     Folders the census is written for. Matches the pass job's range: Phoenix 2 prices
    ///     everything below 10 at zero, and no folder under it has ever been browsed as a tier
    ///     list. Pools themselves are built from every level, so a low chart that does reach
    ///     someone's top 50 still displaces correctly — it just never gets a folder of its own.
    /// </summary>
    private const int LowestFolder = 10;

    private const int PoolSize = 50;

    private readonly IChartRepository _charts;
    private readonly IPumbilityCensusRepository _census;
    private readonly IScoreReader _scores;
    private readonly ITitleRepository _titles;

    public PumbilityCensusSaga(IChartRepository charts, IScoreReader scores, ITitleRepository titles,
        IPumbilityCensusRepository census)
    {
        _charts = charts;
        _scores = scores;
        _titles = titles;
        _census = census;
    }

    public async Task Consume(ConsumeContext<ProcessPumbilityCensusCommand> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var allCharts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        var poolsByType = new Dictionary<ChartType, IReadOnlyDictionary<Guid, PlayerPool>>();
        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            poolsByType[chartType] = await BuildPools(mix, chartType, allCharts, scoring, cancellationToken);

        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
        {
            var pools = poolsByType[chartType];
            var cohorts = await ResolveCohorts(mix, chartType, pools, cancellationToken);
            var holders = HoldersByChart(pools);

            for (var level = LowestFolder; level <= (int)DifficultyLevel.Max; level++)
            {
                var folderCharts = allCharts.Values
                    .Where(c => c.Type == chartType && (int)c.Level == level)
                    .Select(c => c.Id).ToArray();
                if (!folderCharts.Any()) continue;

                var byCohort = new Dictionary<string, PumbilityCensusFolder>();
                foreach (var (cohortKey, members) in cohorts)
                {
                    var counts = folderCharts.ToDictionary(id => id,
                        id => holders.TryGetValue(id, out var who) ? who.Count(members.Contains) : 0);
                    // A cohort whose pools reach nothing here does not get a row set. Writing a
                    // full folder of zeros for every cohort that cannot reach it would be most
                    // of the table — a cohort only covers a three-to-four level band.
                    if (counts.Values.Sum() == 0) continue;

                    byCohort[cohortKey] = new PumbilityCensusFolder(TierListProcessor
                        .ProcessIntoLogScaledTierList(ListName, counts)
                        .Select(e => new PumbilityCensusRecord(e.ChartId, counts[e.ChartId], e.Category, e.Order))
                        .ToArray(), members.Count);
                }

                await _census.SaveFolder(mix, chartType, DifficultyLevel.From(level), byCohort,
                    cancellationToken);
            }
        }
    }

    /// <summary>
    ///     Every player's top-50 for one chart type, priced by the mix's own PUMBILITY formula.
    ///     Read level by level because that is the bulk shape the score store offers.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, PlayerPool>> BuildPools(MixEnum mix, ChartType chartType,
        IReadOnlyDictionary<Guid, Chart> allCharts, ScoringConfiguration scoring,
        CancellationToken cancellationToken)
    {
        var rated = new Dictionary<Guid, List<(Guid ChartId, double Rating)>>();
        for (var level = (int)DifficultyLevel.Min; level <= (int)DifficultyLevel.Max; level++)
        foreach (var (userId, record) in await _scores.GetScores(mix, chartType, DifficultyLevel.From(level),
                     cancellationToken))
        {
            if (record.Score == null || !allCharts.TryGetValue(record.ChartId, out var chart)) continue;
            var rating = scoring.GetScore(chart, record.Score.Value, record.Plate ?? PhoenixPlate.RoughGame,
                record.IsBroken);
            if (rating <= 0) continue;
            if (!rated.TryGetValue(userId, out var scores)) rated[userId] = scores = new List<(Guid, double)>();
            scores.Add((record.ChartId, rating));
        }

        return rated.ToDictionary(kv => kv.Key, kv =>
        {
            var top = kv.Value.OrderByDescending(s => s.Rating).Take(PoolSize).ToArray();
            return new PlayerPool(top.Select(s => s.ChartId).ToHashSet(), top.Sum(s => s.Rating));
        });
    }

    /// <summary>Which of a folder's charts each player pools, inverted so a count is one scan.</summary>
    private static IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> HoldersByChart(
        IReadOnlyDictionary<Guid, PlayerPool> pools)
    {
        var holders = new Dictionary<Guid, List<Guid>>();
        foreach (var (userId, pool) in pools)
        foreach (var chartId in pool.ChartIds)
        {
            if (!holders.TryGetValue(chartId, out var who)) holders[chartId] = who = new List<Guid>();
            who.Add(userId);
        }

        return holders.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Guid>)kv.Value);
    }

    /// <summary>
    ///     The cohorts to count over, plus the community cohort that is every player at once.
    ///     Phoenix 1 groups by the level of a player's highest difficulty title; Phoenix 2 by
    ///     the PUMBILITY rung their pool total clears.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlySet<Guid>>> ResolveCohorts(MixEnum mix,
        ChartType chartType, IReadOnlyDictionary<Guid, PlayerPool> pools,
        CancellationToken cancellationToken)
    {
        var cohorts = new Dictionary<string, IReadOnlySet<Guid>>
        {
            [PumbilityCohortKeys.Community] = pools.Keys.ToHashSet()
        };

        if (mix == MixEnum.Phoenix2)
        {
            var byRung = new Dictionary<string, HashSet<Guid>>();
            foreach (var (userId, pool) in pools)
            {
                var key = PumbilityCohortKeys.ForPhoenix2Pool(chartType, pool.Total);
                if (key == null) continue;
                if (!byRung.TryGetValue(key, out var members)) byRung[key] = members = new HashSet<Guid>();
                members.Add(userId);
            }

            foreach (var (key, members) in byRung) cohorts[key] = members;
            return cohorts;
        }

        for (var level = (int)DifficultyLevel.Min; level <= (int)DifficultyLevel.Max; level++)
        {
            var onLevel = (await _titles.GetUserIdsOnHighestLevel(mix, DifficultyLevel.From(level),
                    cancellationToken))
                .Where(pools.ContainsKey).ToHashSet();
            if (onLevel.Any()) cohorts[PumbilityCohortKeys.ForDifficultyTitleLevel(level)] = onLevel;
        }

        return cohorts;
    }

    /// <summary>A player's top 50 for one chart type: what is in it, and what it sums to.</summary>
    private sealed record PlayerPool(IReadOnlySet<Guid> ChartIds, double Total);
}
