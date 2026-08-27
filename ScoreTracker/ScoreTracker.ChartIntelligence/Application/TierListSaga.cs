using ScoreTracker.Domain.Services;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Domain;
using MassTransit;
using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

internal sealed class TierListSaga : IConsumer<ChartDifficultyUpdatedEvent>,
    IConsumer<ProcessScoresTiersListCommand>,
    IConsumer<ProcessPassTierListCommand>,
    IConsumer<ProcessPumbilityTierListCommand>,
    IConsumer<ProcessSpeedTierListCommand>,
    IRequestHandler<GetMyRelativeTierListQuery, IEnumerable<SongTierListEntry>>,
    IRequestHandler<GetFolderCohortStatsQuery, FolderCohortSummaryRecord?>
{
    private readonly IChartDifficultyRatingRepository _chartRatings;
    private readonly IChartRepository _chartRepository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly ITierListRepository _tierLists;
    private readonly IChartScoringLevelRepository _scoringLevels;
    private readonly IChartScoreStatsRepository _chartStats;
    private readonly IFolderCohortStatsRepository _cohortStats;
    private readonly ITitleRepository _titles;
    private readonly IPumbilityPoolCompositionRepository _composition;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IMediator _mediator;

    public TierListSaga(IChartDifficultyRatingRepository chartRatings, IChartRepository chartRepository,
        ITierListRepository tierLists, IScoreReader scores,
        ICurrentUserAccessor currentUser, IPlayerStatsReader playerStats,
        IChartScoringLevelRepository scoringLevels, IChartScoreStatsRepository chartStats,
        IFolderCohortStatsRepository cohortStats, ITitleRepository titles,
        IPumbilityPoolCompositionRepository composition, IDateTimeOffsetAccessor clock, IMediator mediator)
    {
        _mediator = mediator;
        _composition = composition;
        _clock = clock;
        _cohortStats = cohortStats;
        _chartStats = chartStats;
        _scoringLevels = scoringLevels;
        _chartRatings = chartRatings;
        _chartRepository = chartRepository;
        _tierLists = tierLists;
        _scores = scores;
        _currentUser = currentUser;
        _playerStats = playerStats;
        _titles = titles;
    }


    public async Task Consume(ConsumeContext<ChartDifficultyUpdatedEvent> context)
    {
        var cancellationToken = context.CancellationToken;
        var mix = context.Message.Mix;
        var charts = (await _chartRepository.GetCharts(mix, context.Message.Level,
                context.Message.ChartType, cancellationToken: cancellationToken))
            .ToArray();
        var ratings = (await _chartRatings.GetAllChartRatedDifficulties(mix, cancellationToken))
            .ToDictionary(r => r.ChartId);
        var order = 0;
        foreach (var chart in charts)
        {
            if (!ratings.ContainsKey(chart.Id)) continue;

            var rating = ratings[chart.Id];

            var diff = rating.Difficulty - chart.Level - .5;
            switch (diff)
            {
                case <= -.75:
                    await _tierLists.SaveEntry(mix,
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Overrated, order),
                        cancellationToken);
                    break;
                case <= -.375:
                    await _tierLists.SaveEntry(mix,
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.VeryEasy, order),
                        cancellationToken);
                    break;
                case <= -.125:
                    await _tierLists.SaveEntry(mix,
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Easy, order),
                        cancellationToken);
                    break;
                case < .125:
                    await _tierLists.SaveEntry(mix,
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Medium, order),
                        cancellationToken);
                    break;
                case < .375:
                    await _tierLists.SaveEntry(mix,
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Hard, order),
                        cancellationToken);
                    break;
                case < .75:
                    await _tierLists.SaveEntry(mix,
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.VeryHard, order),
                        cancellationToken);
                    break;
                default:
                    await _tierLists.SaveEntry(mix,
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Underrated, order),
                        cancellationToken);
                    break;
            }

            order++;
        }
    }

    public async Task Consume(ConsumeContext<ProcessPassTierListCommand> context)
    {
        var mix = context.Message.Mix;
        // Levels 10 through 29 — DifficultyLevel.Max. The peer group guards below stop reaching
        // above 29 for the upper folders rather than the loop stopping short of them.
        foreach (var level in Enumerable.Range(10, 20))
        {
            await ProcessPgTierList(mix, level, ChartType.Single, context.CancellationToken);
            await ProcessPgTierList(mix, level, ChartType.Double, context.CancellationToken);

            await ProcessPassTierList(mix, level, ChartType.Single, context.CancellationToken);
            await ProcessPassTierList(mix, level, ChartType.Double, context.CancellationToken);
        }

        foreach (var playerCount in Enumerable.Range(2, 5))
        {
            await ProcessPgTierList(mix, playerCount, ChartType.CoOp, context.CancellationToken);
            await ProcessCoOpPassTierList(mix, playerCount, context.CancellationToken);
        }
    }

    public async Task Consume(ConsumeContext<ProcessScoresTiersListCommand> context)
    {
        var mix = context.Message.Mix;
        for (var level = 1; level <= 29; level++)
            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            {
                var folderRecords = (await _scores.GetScores(mix, chartType, level,
                    context.CancellationToken)).ToArray();
                var allPhoenixScores = folderRecords
                    .Where(s => s.Record.Score != null)
                    .GroupBy(r => r.UserId).ToDictionary(g => g.Key,
                        g => (IDictionary<Guid, PhoenixScore>)g.ToDictionary(p => p.Record.ChartId,
                            p => p.Record.Score!.Value));
                var playerLevels =
                    (await _playerStats.GetStats(mix, folderRecords.Select(r => r.UserId).Distinct().ToArray(),
                        context.CancellationToken))
                    .ToDictionary(s => s.UserId, s => chartType is ChartType.Single
                        ? s.SinglesCompetitiveLevel
                        : s.DoublesCompetitiveLevel);
                var stats = allPhoenixScores.Keys.ToDictionary(id => id, id => level + .5 - playerLevels[id]);

                var weights = stats.ToDictionary(kv => kv.Key.ToString(), kv => Math.Pow(.5, Math.Abs(kv.Value)));
                var results =
                    TierListProcessor.ProcessIntoTierList(allPhoenixScores.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value), level,
                        "Scores", weights);
                await _tierLists.SaveEntries(mix, results, context.CancellationToken);

                // Materialized population variance (tier-lists overhaul C1, design doc §6
                // Tier 1): per-chart score stddev over comparable players — the same
                // ±2-levels-of-the-folder band the page-side variance used. Readers apply
                // their own minimum-count rule; below two scores nothing is computable.
                // stats[u] = level + .5 - competitiveLevel, so |comp - level| < 2 ⇔ |.5 - stats[u]| < 2.
                var comparableScores = new Dictionary<Guid, List<int>>();
                foreach (var (userId, userScores) in allPhoenixScores)
                {
                    if (Math.Abs(.5 - stats[userId]) >= 2.0) continue;
                    foreach (var (chartId, score) in userScores)
                    {
                        if (!comparableScores.TryGetValue(chartId, out var chartScoreList))
                            comparableScores[chartId] = chartScoreList = new List<int>();
                        chartScoreList.Add(score);
                    }
                }

                var statEntries = comparableScores.Where(kv => kv.Value.Count >= 2)
                    .Select(kv => new ChartScoreStatsRecord(kv.Key,
                        TierListProcessor.StdDev(kv.Value, true), kv.Value.Count))
                    .ToArray();
                if (statEntries.Any())
                    await _chartStats.SaveStats(mix, statEntries, context.CancellationToken);

                // Round 7: folder pass-count histograms per half-level competitive bucket —
                // powers the "Folder Passes vs Similar Players" strip bar. Everyone with a
                // record in the folder counts (a broken attempt is 0 passes); read-time
                // merges of neighboring buckets reproduce the ±0.5 similar-players window.
                var passBuckets = folderRecords
                    .GroupBy(r => r.UserId)
                    .Select(g => (Bucket: (int)Math.Round(playerLevels[g.Key] * 2),
                        Passes: g.Count(r => !r.Record.IsBroken)))
                    .GroupBy(p => p.Bucket)
                    .Select(g => new FolderCohortBucketRecord(g.Key,
                        g.GroupBy(p => p.Passes).ToDictionary(h => h.Key, h => h.Count())))
                    .ToArray();
                if (passBuckets.Any())
                    await _cohortStats.SaveFolder(mix, chartType, level, passBuckets, context.CancellationToken);
            }
    }

    /// <summary>
    ///     The Speed tier list (docs/design/chart-identity.md §2): every folder's charts banded
    ///     against their own folder's notes-per-second spread. The measurement comes from the
    ///     banked step analysis through Catalog's published contract — this vertical never
    ///     reads another's tables — and a chart piucenter has nothing for simply is not banded.
    /// </summary>
    public async Task Consume(ConsumeContext<ProcessSpeedTierListCommand> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;
        var charts = (await _chartRepository.GetCharts(mix, cancellationToken: cancellationToken)).ToArray();
        if (charts.Length == 0) return;

        var analyses = await _mediator.Send(new GetChartStepAnalysesQuery(charts.Select(c => c.Id).ToArray()),
            cancellationToken);

        var entries = charts
            .Where(c => analyses.TryGetValue(c.Id, out var analysis) && analysis.Nps != null)
            .GroupBy(c => (c.Type, Level: (int)c.Level))
            .SelectMany(folder => SpeedBands.Band(folder
                .Select(c => (c.Id, analyses[c.Id].Nps!.Value))
                .ToArray()))
            .ToArray();

        if (entries.Length > 0) await _tierLists.SaveEntries(mix, entries, cancellationToken);
    }

    public async Task<FolderCohortSummaryRecord?> Handle(GetFolderCohortStatsQuery request,
        CancellationToken cancellationToken)
    {
        var buckets = (await _cohortStats.GetBuckets(request.Mix, request.ChartType, request.Level,
                cancellationToken))
            .Where(b => Math.Abs(b.Bucket / 2.0 - request.CompetitiveLevel) <= .5)
            .ToArray();
        var players = buckets.Sum(b => b.PassHistogram.Values.Sum());
        if (players == 0) return null;

        var totalPasses = buckets.Sum(b => b.PassHistogram.Sum(kv => (long)kv.Key * kv.Value));
        var atOrBelow = buckets.Sum(b => b.PassHistogram.Where(kv => kv.Key <= request.PassCount)
            .Sum(kv => kv.Value));
        return new FolderCohortSummaryRecord(players, totalPasses / (double)players,
            atOrBelow / (double)players);
    }

    public async Task<IEnumerable<SongTierListEntry>> Handle(GetMyRelativeTierListQuery request,
        CancellationToken cancellationToken)
    {
        var filtered = await _chartRepository.GetCharts(request.Mix, request.Level, request.ChartType,
            cancellationToken: cancellationToken);
        var phoenixScores =
            (await _scores.GetBestScores(request.Mix, request.UserId ?? _currentUser.User.Id, cancellationToken))
            .ToDictionary(
                s => s.ChartId);


        var filteredCompareScoreArray = filtered
            .Where(c => phoenixScores.ContainsKey(c.Id) && phoenixScores[c.Id].Score != null)
            .OrderBy(c => phoenixScores.ContainsKey(c.Id) ? (int)(phoenixScores[c.Id]?.Score ?? 0) : 0).ToArray();
        if (!filteredCompareScoreArray.Any()) return Array.Empty<SongTierListEntry>();

        var officialScoreTierListEntries =
            (await _tierLists.GetAllEntries(request.Mix, request.Level >= 24 ? "Official Scores" : "Scores",
                cancellationToken))
            .ToDictionary(e => e.ChartId);
        var standardDeviationCompare =
            TierListProcessor.StdDev(filteredCompareScoreArray.Select(s => (int)(phoenixScores[s.Id].Score ?? 0)), true);
        var averageCompare = filteredCompareScoreArray.Average(s => phoenixScores[s.Id]?.Score ?? 0);
        var mediumMinCompare = averageCompare - standardDeviationCompare / 2;
        var easyMinCompare = averageCompare + standardDeviationCompare / 2;
        var veryEasyMinCompare = averageCompare + standardDeviationCompare;
        var oneLevelOverratedCompare = averageCompare + standardDeviationCompare * 1.5;
        var hardMinCompare = averageCompare - standardDeviationCompare;
        var veryHardMinCompare = averageCompare - standardDeviationCompare * 1.5;
        var result = new List<SongTierListEntry>();
        foreach (var chart in filteredCompareScoreArray)
        {
            if (!officialScoreTierListEntries.TryGetValue(chart.Id, out var officialEntry)) continue;
            var score = (int)(phoenixScores[chart.Id]?.Score ?? 0);
            var myCategory = TierListCategory.Overrated;
            if (score < veryHardMinCompare)
                myCategory = TierListCategory.Underrated;
            else if (score < hardMinCompare)
                myCategory = TierListCategory.VeryHard;
            else if (score < mediumMinCompare)
                myCategory = TierListCategory.Hard;
            else if (score < easyMinCompare)
                myCategory = TierListCategory.Medium;
            else if (score < veryEasyMinCompare)
                myCategory = TierListCategory.Easy;
            else if (score < oneLevelOverratedCompare)
                myCategory = TierListCategory.VeryEasy;
            else
                myCategory = TierListCategory.Overrated;
            var diff = officialEntry.Category - myCategory;
            switch (diff)
            {
                case > 2:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Overrated,
                        diff * -100));
                    break;
                case > 1:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.VeryEasy,
                        diff * -100));
                    break;
                case > 0:
                    result.Add(
                        new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Easy, diff * -100));
                    break;
                case > -1:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Medium,
                        diff * -100));
                    break;
                case > -2:
                    result.Add(
                        new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Hard, diff * -100));
                    break;
                case > -3:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.VeryHard,
                        diff * -100));
                    break;
                default:
                    result.Add(new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Underrated,
                        diff * -100));
                    break;
            }
        }

        return result;
    }

    private async Task ProcessCoOpPassTierList(MixEnum mix, int playerCount, CancellationToken cancellationToken)
    {
        var scores = (await _scores.GetScores(mix, ChartType.CoOp, playerCount, cancellationToken))
            .Where(s => s.Record is { Score: not null, IsBroken: false }).ToArray();
        var playerLevels =
            (await _playerStats.GetStats(mix, scores.Select(s => s.UserId).Distinct().ToArray(), cancellationToken))
            .ToDictionary(u => u.UserId, u => u.DoublesCompetitiveLevel);
        var playerWeights = playerLevels.ToDictionary(u => u.Key, u => Math.Log(28.0 - u.Value));

        var chartTotals = scores.GroupBy(s => s.Record.ChartId)
            .ToDictionary(g => g.Key, g => g.Sum(r => playerWeights[r.UserId]));
        var entries = TierListProcessor.ProcessIntoTierList("Pass Count", chartTotals);

        var chartMinimums = scores.GroupBy(s => s.Record.ChartId)
            .ToDictionary(g => g.Key, g => g.Min(r => playerLevels[r.UserId]));

        await _tierLists.SaveEntries(mix, entries, cancellationToken);
        foreach (var kv in chartMinimums)
            await _scoringLevels.SaveScoringLevel(mix, kv.Key, kv.Value, cancellationToken);
    }

    private async Task ProcessPgTierList(MixEnum mix, DifficultyLevel level, ChartType chartType,
        CancellationToken cancellationToken)
    {
        var charts =
            (await _chartRepository.GetCharts(mix, level, chartType, cancellationToken: cancellationToken))
            .ToArray();
        var pgUsers = (await _scores.GetPgUsers(mix, chartType, level, cancellationToken)).ToArray();

        var stats = (await _playerStats.GetStats(mix, pgUsers.Select(p => p.UserId).Distinct(), cancellationToken))
            .ToDictionary(s => s.UserId);

        var pgSums = charts.ToDictionary(c => c.Id, c => 0.0);
        foreach (var record in pgUsers)
        {
            var competitiveLevel = chartType == ChartType.Single
                ? stats[record.UserId].SinglesCompetitiveLevel
                : stats[record.UserId].DoublesCompetitiveLevel;
            if (competitiveLevel < 5)
                continue;
            pgSums[record.ChartId] += Math.Pow(1.25, level + .5 - competitiveLevel);
        }

        if (!pgSums.Any()) return;


        var result = new List<SongTierListEntry>();
        result.AddRange(TierListProcessor.ProcessIntoTierList("PG",
            pgSums.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value)));
        await _tierLists.SaveEntries(mix, result, cancellationToken);
    }

    private async Task ProcessPassTierList(MixEnum mix, DifficultyLevel level, ChartType chartType,
        CancellationToken cancellationToken)
    {
        var charts =
            (await _chartRepository.GetCharts(mix, level, chartType, cancellationToken: cancellationToken))
            .ToArray();
        var userWeights = new Dictionary<int, IEnumerable<Guid>>
        {
            { 7, await _tierLists.GetUsersOnLevel(mix, level - 3, cancellationToken, true) },
            { 6, await _tierLists.GetUsersOnLevel(mix, level - 2, cancellationToken, true) },
            { 5, await _tierLists.GetUsersOnLevel(mix, level - 1, cancellationToken, true) },
            { 4, await _tierLists.GetUsersOnLevel(mix, level, cancellationToken, true) }
        };
        if (level < 27) userWeights[3] = await _tierLists.GetUsersOnLevel(mix, level + 3, cancellationToken);
        if (level < 28) userWeights[2] = await _tierLists.GetUsersOnLevel(mix, level + 2, cancellationToken);
        if (level < 29) userWeights[1] = await _tierLists.GetUsersOnLevel(mix, level + 1, cancellationToken);
        var chartSums = charts.ToDictionary(c => c.Id, c => 0);
        foreach (var weightValue in userWeights)
        {
            var scores =
                (await _scores.GetScores(mix, weightValue.Value, chartType, level, level,
                    cancellationToken))
                .Where(s => !s.IsBroken).ToArray();

            foreach (var score in scores.Where(s => chartSums.ContainsKey(s.ChartId)))
                chartSums[score.ChartId] += weightValue.Key;
        }

        if (!chartSums.Any()) return;


        var result = new List<SongTierListEntry>();
        result.AddRange(TierListProcessor.ProcessIntoTierList("Pass Count", chartSums));
        await _tierLists.SaveEntries(mix, result, cancellationToken);
    }

    // --- The PUMBILITY tier lists (docs/design/pumbility-tier-list.md) -----------------------
    // For every folder and every peer group, how many of that peer group's top-50 pools hold each
    // chart. Pools are built once per chart type from a level-by-level bulk read rather than
    // per-player — the alternative is roughly three thousand round trips for a job that only
    // ever wants every pool at once.

    /// <summary>The list name the blend reads the PUMBILITY source under.</summary>
    internal const string PumbilityListName = "PUMBILITY";

    /// <summary>
    ///     Folders the PUMBILITY lists are written for. Matches the pass job's range: Phoenix 2
    ///     prices everything below 10 at zero, and no folder under it has ever been browsed as a
    ///     tier list. Pools themselves are built from every level, so a low chart that does
    ///     reach someone's top 50 still displaces correctly — it just never gets a folder of
    ///     its own.
    /// </summary>
    private static readonly int LowestPumbilityFolder = (int)PeerGroup.PumbilityPoolFloor;

    public async Task Consume(ConsumeContext<ProcessPumbilityTierListCommand> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var allCharts = (await _chartRepository.GetCharts(mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        // One read of the scores serves both products: the per-type pools the tier lists count
        // over, and the merged Singles+Doubles pools the population composition is built from —
        // the latter being the pool a player's total and title rung actually come from
        // (docs/design/pumbility-calculator.md D10).
        var ratedByType = new Dictionary<ChartType, IReadOnlyDictionary<Guid, List<RatedChart>>>();
        foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
        {
            var rated = await RateCharts(mix, chartType, allCharts, scoring, cancellationToken);
            ratedByType[chartType] = rated;
            var pools = PoolsFrom(rated);
            var peerGroups = await ResolvePeers(mix, chartType, pools, cancellationToken);
            var holders = HoldersByChart(pools);

            for (var level = LowestPumbilityFolder; level <= (int)DifficultyLevel.Max; level++)
            {
                var folderCharts = allCharts.Values
                    .Where(c => c.Type == chartType && (int)c.Level == level)
                    .Select(c => c.Id).ToArray();
                if (!folderCharts.Any()) continue;

                var byPeerKey = new Dictionary<string, PumbilityTierListFolder>();
                foreach (var (peerKey, members) in peerGroups)
                {
                    var counts = folderCharts.ToDictionary(id => id,
                        id => holders.TryGetValue(id, out var who) ? who.Count(members.Contains) : 0);
                    // A peer group whose pools reach nothing here does not get a row set. Writing a
                    // full folder of zeros for every peer group that cannot reach it would be most
                    // of the table — a peer group only covers a three-to-four level band.
                    if (counts.Values.Sum() == 0) continue;

                    // Counted over every member, the reader included when they are one: the list is
                    // one per peer group, and a player is never one of their own peers, so the
                    // reader takes their own pool back out at read time (TierListBlendBuilder).
                    byPeerKey[peerKey] = new PumbilityTierListFolder(TierListProcessor
                        .ProcessIntoLogScaledTierList(PumbilityListName, counts)
                        .Select(e => new PumbilityTierListRecord(e.ChartId, counts[e.ChartId], e.Category, e.Order))
                        .ToArray(), members.Count);
                }

                await _tierLists.SavePumbilityTierLists(mix, chartType, DifficultyLevel.From(level), byPeerKey,
                    cancellationToken);
            }
        }

        await _composition.Save(BuildComposition(mix, ratedByType.Values), cancellationToken);
    }

    /// <summary>
    ///     Every player's charts of one type, priced by the mix's own PUMBILITY formula and split
    ///     into the parts the population section sums. Read level by level because that is the
    ///     bulk shape the score store offers. Charts worth nothing (broken, sub-10, excluded types)
    ///     are left out here, so a pool can only ever hold contributing charts.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, List<RatedChart>>> RateCharts(MixEnum mix, ChartType chartType,
        IReadOnlyDictionary<Guid, Chart> allCharts, ScoringConfiguration scoring,
        CancellationToken cancellationToken)
    {
        var rated = new Dictionary<Guid, List<RatedChart>>();
        for (var level = (int)DifficultyLevel.Min; level <= (int)DifficultyLevel.Max; level++)
        foreach (var (userId, record) in await _scores.GetScores(mix, chartType, DifficultyLevel.From(level),
                     cancellationToken))
        {
            if (record.Score == null || !allCharts.TryGetValue(record.ChartId, out var chart)) continue;
            var plate = record.Plate ?? PhoenixPlate.RoughGame;
            var rating = scoring.GetScore(chart, record.Score.Value, plate, record.IsBroken);
            if (rating <= 0) continue;
            if (!rated.TryGetValue(userId, out var scores)) rated[userId] = scores = new List<RatedChart>();
            scores.Add(new RatedChart(record.ChartId, rating,
                new PooledChart((int)chart.Level, record.Score.Value.LetterGradeFor(mix),
                    scoring.Decompose(chart, record.Score.Value, plate, record.IsBroken))));
        }

        return rated;
    }

    /// <summary>
    ///     Every player's top-50 for one chart type, through the one definition of a pool
    ///     (<see cref="PumbilityPeers.TopPool" />) so the writer and the reader cannot disagree
    ///     about what one is. Only full pools count. A player with thirty charts has a low total
    ///     because they have played thirty charts, not because they are weak, and letting that
    ///     stand puts them in a peer group of genuinely weaker players — which is exactly what a
    ///     mix with no score volume yet produces for everyone. It also keeps Phoenix 2 dark until
    ///     its pools are real, and lights it up on its own as they fill
    ///     (docs/design/pumbility-tier-list.md §8).
    /// </summary>
    private static IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> PoolsFrom(
        IReadOnlyDictionary<Guid, List<RatedChart>> rated)
    {
        var pools = new Dictionary<Guid, IReadOnlySet<Guid>>();
        foreach (var (userId, scores) in rated)
            if (PumbilityPeers.TopPool(scores.Select(s => (s.ChartId, s.Rating))) is { } pool)
                pools[userId] = pool;
        return pools;
    }

    /// <summary>
    ///     The population composition: every player's merged Singles+Doubles top-50 — the pool their
    ///     total and title rung come from — accumulated per band. The same full-pool gate applies,
    ///     to the merged fifty: a player short of fifty contributing charts across both types has
    ///     no pool to be counted in yet.
    /// </summary>
    private PumbilityPoolCompositionRecord BuildComposition(MixEnum mix,
        IEnumerable<IReadOnlyDictionary<Guid, List<RatedChart>>> ratedByType)
    {
        var merged = new Dictionary<Guid, List<RatedChart>>();
        foreach (var rated in ratedByType)
        foreach (var (userId, charts) in rated)
        {
            if (!merged.TryGetValue(userId, out var all)) merged[userId] = all = new List<RatedChart>();
            all.AddRange(charts);
        }

        var builder = new PumbilityPoolCompositionBuilder(mix);
        foreach (var charts in merged.Values.Where(c => c.Count >= PumbilityPeers.PoolSize))
            builder.Add(TopFifty(charts).Select(c => c.Pooled).ToArray());
        return builder.Build(_clock.Now);
    }

    private static RatedChart[] TopFifty(IEnumerable<RatedChart> charts)
    {
        return charts.OrderByDescending(c => c.Rating).Take(PumbilityPeers.PoolSize).ToArray();
    }

    /// <summary>Which of a folder's charts each player pools, inverted so a count is one scan.</summary>
    private static IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> HoldersByChart(
        IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> pools)
    {
        var holders = new Dictionary<Guid, List<Guid>>();
        foreach (var (userId, pool) in pools)
        foreach (var chartId in pool)
        {
            if (!holders.TryGetValue(chartId, out var who)) holders[chartId] = who = new List<Guid>();
            who.Add(userId);
        }

        return holders.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Guid>)kv.Value);
    }

    /// <summary>
    ///     The peer groups to count over, plus the community group that is every player at once.
    ///     Phoenix 1 groups by the level of a player's highest difficulty title; Phoenix 2 by the
    ///     viewer's PUMBILITY rung, each group being the band of three rungs either side of it.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlySet<Guid>>> ResolvePeers(MixEnum mix,
        ChartType chartType, IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> pools,
        CancellationToken cancellationToken)
    {
        var peerGroups = new Dictionary<string, IReadOnlySet<Guid>>
        {
            [PumbilityPeers.Community] = pools.Keys.ToHashSet()
        };

        if (mix == MixEnum.Phoenix2)
        {
            // PUMBILITY peers (docs/design/pumbility-tier-list.md §5, the same definition the
            // projection uses): a list per VIEWER rung, counted over every full-pool player within
            // three rungs of it. The rung is the total pool's — the merged top fifty across both
            // types, the number the game's badge is drawn from — read from stats rather than from
            // the per-type pool this pass built, because that pool is only one type's half of it.
            var rungOf = (await _playerStats.GetStats(mix, pools.Keys, cancellationToken))
                .ToDictionary(s => s.UserId, s => Phoenix2PumbilityLevel.From(s.SkillRating).Index);
            for (var rung = 0; rung <= Phoenix2PumbilityLevel.CapstoneIndex; rung++)
            {
                var (lowest, highest) = PumbilityPeers.Phoenix2Band(rung);
                var members = pools.Keys
                    .Where(id => rungOf.TryGetValue(id, out var r) && r >= lowest && r <= highest)
                    .ToHashSet();
                if (members.Count > 0) peerGroups[PumbilityPeers.ForPhoenix2Rung(rung)] = members;
            }

            return peerGroups;
        }

        for (var level = (int)DifficultyLevel.Min; level <= (int)DifficultyLevel.Max; level++)
        {
            var onLevel = (await _titles.GetUserIdsOnHighestLevel(mix, DifficultyLevel.From(level),
                    cancellationToken))
                .Where(pools.ContainsKey).ToHashSet();
            if (onLevel.Any()) peerGroups[PumbilityPeers.ForDifficultyTitleLevel(level)] = onLevel;
        }

        return peerGroups;
    }

    /// <summary>One priced chart of a player's, with the composition's view of it alongside the rating.</summary>
    private sealed record RatedChart(Guid ChartId, double Rating, PooledChart Pooled);
}
