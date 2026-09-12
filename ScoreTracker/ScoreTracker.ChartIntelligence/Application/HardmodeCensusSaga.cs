using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     The weekly Hardmode census (docs/design/hardmode-leaderboard.md §1): which charts almost
///     nobody holds in a top 50, folder by folder.
///     <para>
///         It walks every full fifty on the mix — site accounts through <see cref="IScoreReader" />
///         and official-board players through <see cref="IOfficialPoolReader" /> — adds each
///         chart's slot weight, and hands each folder to <see cref="HardmodeCut" />. Then it
///         replaces the list and says so, which is what starts the two rating writers.
///     </para>
/// </summary>
internal sealed class HardmodeCensusSaga :
    IConsumer<RebuildHardmodeChartsCommand>,
    IRequestHandler<GetHardmodeChartsQuery, IReadOnlyList<HardmodeChartRecord>>
{
    private readonly IBus _bus;
    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly ILogger<HardmodeCensusSaga> _logger;
    private readonly IOfficialPoolReader _officialPools;
    private readonly IHardmodeChartRepository _repository;
    private readonly IChartScoringLevelRepository _scoringLevels;
    private readonly IScoreReader _scores;

    public HardmodeCensusSaga(IHardmodeChartRepository repository, IScoreReader scores,
        IOfficialPoolReader officialPools, IChartRepository charts, IChartScoringLevelRepository scoringLevels,
        IDateTimeOffsetAccessor clock, IBus bus, ILogger<HardmodeCensusSaga> logger)
    {
        _repository = repository;
        _scores = scores;
        _officialPools = officialPools;
        _charts = charts;
        _scoringLevels = scoringLevels;
        _clock = clock;
        _bus = bus;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RebuildHardmodeChartsCommand> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;

        // A chart the mix's own PUMBILITY prices at zero is not an opportunity, so it is not a
        // candidate (owner, 2026-09-12). On Phoenix 2 that is everything below level 10, and
        // leaving them in was not a rounding error: nobody can be credited with holding one, so
        // every sub-10 folder read 100% unheld, the cut rule opened all the way, and the page
        // offered whole folders as the rarest thing in the game — at 0.00 a play. Asking the
        // scoring configuration rather than writing "level >= 10" keeps the rule true if a mix
        // ever prices a different floor.
        var catalogScoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .Where(c => c.Type is ChartType.Single or ChartType.Double)
            .Where(c => CanEverScore(catalogScoring, c))
            .ToDictionary(c => c.Id);
        if (charts.Count == 0)
        {
            _logger.LogInformation("Hardmode census skipped for {Mix}: the mix has no catalog", mix);
            return;
        }

        var scoringLevels = await _scoringLevels.GetScoringLevels(mix, cancellationToken);
        var weights = new Dictionary<Guid, double>();
        var holders = new Dictionary<Guid, HashSet<string>>();
        var pools = 0;

        pools += await CountSitePools(mix, charts, weights, holders, cancellationToken);
        pools += await CountOfficialPools(mix, charts, weights, holders, cancellationToken);

        // A mix nobody has a full pool on has no census to take. Writing nothing leaves the last
        // good list in place; writing the result would declare every chart in the game rare.
        if (pools == 0)
        {
            _logger.LogInformation("Hardmode census skipped for {Mix}: no full pools to count", mix);
            return;
        }

        var qualifying = charts.Values
            .GroupBy(c => (c.Type, Level: (int)c.Level))
            .SelectMany(folder =>
            {
                var candidates = folder.Select(c => new HardmodeCandidate(c.Id, c.Song.Name.ToString(), c.Type,
                    (int)c.Level, scoringLevels.TryGetValue(c.Id, out var level) && level > 0 ? level : null,
                    weights.GetValueOrDefault(c.Id),
                    holders.TryGetValue(c.Id, out var held) ? held.Count : 0)).ToArray();
                var size = candidates.Length;
                var cut = HardmodeCut.Qualifying(candidates);
                return cut.Select(c => new HardmodeChartRecord(c.ChartId, c.ChartType, c.Level, c.Points,
                    c.Holders, size, cut.Count));
            })
            .ToArray();

        await _repository.Replace(mix, qualifying, _clock.Now, cancellationToken);
        // Pools, not players: one account with a full singles AND doubles record contributes
        // three (combined, singles, doubles), so this reads about 3x the account count the
        // design doc quotes.
        _logger.LogInformation("Hardmode census for {Mix}: {Charts} charts from {Pools} pools", mix,
            qualifying.Length, pools);
        await _bus.Publish(new HardmodeChartsRebuiltEvent(mix, qualifying.Length, pools), cancellationToken);
    }

    public async Task<IReadOnlyList<HardmodeChartRecord>> Handle(GetHardmodeChartsQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.Get(request.Mix, cancellationToken);
    }

    /// <summary>
    ///     Site accounts. One read per chart type per level, which is how the PUMBILITY tier-list
    ///     sweep walks the same population — the scores arrive per level, so the pools are
    ///     assembled here rather than read whole.
    /// </summary>
    /// <summary>
    ///     Whether a perfect game on this chart would be worth anything at all. The census and
    ///     both pricing halves agree on what counts BECAUSE they all ask the same configuration:
    ///     a chart worth zero is skipped by the pricing loops anyway, so one left in the catalog
    ///     could only ever be a chart nobody holds.
    /// </summary>
    private static bool CanEverScore(ScoringConfiguration scoring, Chart chart)
    {
        return scoring.GetScore(chart, PhoenixScore.From(1_000_000), PhoenixPlate.PerfectGame, false) > 0;
    }

    private async Task<int> CountSitePools(MixEnum mix, IReadOnlyDictionary<Guid, Chart> charts,
        IDictionary<Guid, double> weights, IDictionary<Guid, HashSet<string>> holders,
        CancellationToken cancellationToken)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var valued = new Dictionary<Guid, List<(Guid ChartId, ChartType Type, double Value)>>();

        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
        foreach (var level in DifficultyLevel.All)
        {
            var rows = await _scores.GetScores(mix, chartType, level, cancellationToken);
            foreach (var (userId, record) in rows)
            {
                if (record.Score == null || record.IsBroken) continue;
                if (!charts.TryGetValue(record.ChartId, out var chart)) continue;
                var value = scoring.GetScore(chart, record.Score.Value,
                    record.Plate ?? ScoringConfiguration.ExpectedPlateForScore(record.Score.Value), false);
                if (value <= 0) continue;
                (valued.TryGetValue(userId, out var list) ? list : valued[userId] = new()).Add(
                    (record.ChartId, chart.Type, value));
            }
        }

        var counted = 0;
        foreach (var (userId, all) in valued)
            counted += Count($"U:{userId}", all, weights, holders);
        return counted;
    }

    /// <summary>
    ///     Official-board players, whose fifties arrive already assembled and already in pool
    ///     order — the mirror is the only thing that can price a board row, because a board row
    ///     carries no plate.
    /// </summary>
    private async Task<int> CountOfficialPools(MixEnum mix, IReadOnlyDictionary<Guid, Chart> charts,
        IDictionary<Guid, double> weights, IDictionary<Guid, HashSet<string>> holders,
        CancellationToken cancellationToken)
    {
        var counted = 0;
        foreach (var pool in await _officialPools.GetFullPools(mix, cancellationToken))
        {
            if (pool.ChartIds.Count < HardmodeCut.PoolSize) continue;
            var key = $"B:{pool.OfficialPlayerId}";
            for (var i = 0; i < HardmodeCut.PoolSize; i++)
            {
                var chartId = pool.ChartIds[i];
                if (!charts.ContainsKey(chartId)) continue;
                Credit(chartId, HardmodeCut.SlotWeight(i + 1), key, weights, holders);
            }

            counted++;
        }

        return counted;
    }

    /// <summary>
    ///     One player's three pools — combined, singles, doubles — exactly as the official boards
    ///     split them (D7). A pool short of fifty does not vote: its slot 1 would carry 50 points
    ///     off three charts (D6).
    /// </summary>
    private static int Count(string who, IReadOnlyCollection<(Guid ChartId, ChartType Type, double Value)> all,
        IDictionary<Guid, double> weights, IDictionary<Guid, HashSet<string>> holders)
    {
        var counted = 0;
        counted += CountOne($"{who}|All", all, weights, holders);
        counted += CountOne($"{who}|S", all.Where(v => v.Type == ChartType.Single), weights, holders);
        counted += CountOne($"{who}|D", all.Where(v => v.Type == ChartType.Double), weights, holders);
        return counted;
    }

    private static int CountOne(string key, IEnumerable<(Guid ChartId, ChartType Type, double Value)> values,
        IDictionary<Guid, double> weights, IDictionary<Guid, HashSet<string>> holders)
    {
        var fifty = values.OrderByDescending(v => v.Value).ThenBy(v => v.ChartId)
            .Take(HardmodeCut.PoolSize).ToArray();
        if (fifty.Length < HardmodeCut.PoolSize) return 0;
        // The holder key is the player, not the pool, so one player's three pools count once.
        var player = key[..key.IndexOf('|')];
        for (var i = 0; i < fifty.Length; i++)
            Credit(fifty[i].ChartId, HardmodeCut.SlotWeight(i + 1), player, weights, holders);
        return 1;
    }

    private static void Credit(Guid chartId, int weight, string player, IDictionary<Guid, double> weights,
        IDictionary<Guid, HashSet<string>> holders)
    {
        weights[chartId] = weights.TryGetValue(chartId, out var current) ? current + weight : weight;
        if (holders.TryGetValue(chartId, out var set)) set.Add(player);
        else holders[chartId] = new HashSet<string> { player };
    }
}
