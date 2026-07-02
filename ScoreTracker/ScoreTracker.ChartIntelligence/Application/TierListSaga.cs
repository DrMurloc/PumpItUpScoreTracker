using ScoreTracker.Domain.Services;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using MassTransit;
using MediatR;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

internal sealed class TierListSaga : IConsumer<ChartDifficultyUpdatedEvent>,
    IConsumer<ProcessScoresTiersListCommand>,
    IConsumer<ProcessPassTierListCommand>,
    IRequestHandler<GetMyRelativeTierListQuery, IEnumerable<SongTierListEntry>>
{
    private readonly IChartDifficultyRatingRepository _chartRatings;
    private readonly IChartRepository _chartRepository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly ITierListRepository _tierLists;
    private readonly IChartScoringLevelRepository _scoringLevels;

    public TierListSaga(IChartDifficultyRatingRepository chartRatings, IChartRepository chartRepository,
        ITierListRepository tierLists, IScoreReader scores,
        ICurrentUserAccessor currentUser, IPlayerStatsReader playerStats,
        IChartScoringLevelRepository scoringLevels)
    {
        _scoringLevels = scoringLevels;
        _chartRatings = chartRatings;
        _chartRepository = chartRepository;
        _tierLists = tierLists;
        _scores = scores;
        _currentUser = currentUser;
        _playerStats = playerStats;
    }


    public async Task Consume(ConsumeContext<ChartDifficultyUpdatedEvent> context)
    {
        var cancellationToken = context.CancellationToken;
        var charts = (await _chartRepository.GetCharts(MixEnum.Phoenix, context.Message.Level,
                context.Message.ChartType, cancellationToken: cancellationToken))
            .ToArray();
        var ratings = (await _chartRatings.GetAllChartRatedDifficulties(MixEnum.Phoenix, cancellationToken))
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
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Overrated, order),
                        cancellationToken);
                    break;
                case <= -.375:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.VeryEasy, order),
                        cancellationToken);
                    break;
                case <= -.125:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Easy, order),
                        cancellationToken);
                    break;
                case < .125:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Medium, order),
                        cancellationToken);
                    break;
                case < .375:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Hard, order),
                        cancellationToken);
                    break;
                case < .75:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.VeryHard, order),
                        cancellationToken);
                    break;
                default:
                    await _tierLists.SaveEntry(
                        new SongTierListEntry("Difficulty", chart.Id, TierListCategory.Underrated, order),
                        cancellationToken);
                    break;
            }

            order++;
        }
    }

    public async Task Consume(ConsumeContext<ProcessPassTierListCommand> context)
    {
        foreach (var level in Enumerable.Range(10, 18))
        {
            await ProcessPgTierList(level, ChartType.Single, context.CancellationToken);
            await ProcessPgTierList(level, ChartType.Double, context.CancellationToken);

            await ProcessPassTierList(level, ChartType.Single, context.CancellationToken);
            await ProcessPassTierList(level, ChartType.Double, context.CancellationToken);
        }

        foreach (var playerCount in Enumerable.Range(2, 5))
        {
            await ProcessPgTierList(playerCount, ChartType.CoOp, context.CancellationToken);
            await ProcessCoOpPassTierList(playerCount, context.CancellationToken);
        }
    }

    public async Task Consume(ConsumeContext<ProcessScoresTiersListCommand> context)
    {
        for (var level = 1; level <= 29; level++)
            foreach (var chartType in new[] { ChartType.Single, ChartType.Double })
            {
                var allPhoenixScores = (await _scores.GetScores(chartType, level, context.CancellationToken))
                    .Where(s => s.Record.Score != null)
                    .GroupBy(r => r.UserId).ToDictionary(g => g.Key,
                        g => (IDictionary<Guid, PhoenixScore>)g.ToDictionary(p => p.Record.ChartId,
                            p => p.Record.Score!.Value));
                var userIds = allPhoenixScores.Keys;
                var stats = new Dictionary<Guid, double>();
                foreach (var userId in userIds)
                {
                    var record = await _playerStats.GetStats(userId, context.CancellationToken);
                    stats[userId] = level + .5 - (chartType is ChartType.Single
                        ? record.SinglesCompetitiveLevel
                        : record.DoublesCompetitiveLevel);
                }

                var weights = stats.ToDictionary(kv => kv.Key.ToString(), kv => Math.Pow(.5, Math.Abs(kv.Value)));
                var results =
                    TierListProcessor.ProcessIntoTierList(allPhoenixScores.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value), level,
                        "Scores", weights);
                await _tierLists.SaveEntries(results, context.CancellationToken);
            }
    }

    public async Task<IEnumerable<SongTierListEntry>> Handle(GetMyRelativeTierListQuery request,
        CancellationToken cancellationToken)
    {
        var filtered = await _chartRepository.GetCharts(MixEnum.Phoenix, request.Level, request.ChartType,
            cancellationToken: cancellationToken);
        var phoenixScores =
            (await _scores.GetBestScores(request.UserId ?? _currentUser.User.Id, cancellationToken)).ToDictionary(
                s => s.ChartId);


        var filteredCompareScoreArray = filtered
            .Where(c => phoenixScores.ContainsKey(c.Id) && phoenixScores[c.Id].Score != null)
            .OrderBy(c => phoenixScores.ContainsKey(c.Id) ? (int)(phoenixScores[c.Id]?.Score ?? 0) : 0).ToArray();
        if (!filteredCompareScoreArray.Any()) return Array.Empty<SongTierListEntry>();

        var officialScoreTierListEntries =
            (await _tierLists.GetAllEntries(request.Level >= 24 ? "Official Scores" : "Scores", cancellationToken))
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

    private async Task ProcessCoOpPassTierList(int playerCount, CancellationToken cancellationToken)
    {
        var scores = (await _scores.GetScores(ChartType.CoOp, playerCount, cancellationToken))
            .Where(s => s.Record is { Score: not null, IsBroken: false }).ToArray();
        var playerLevels =
            (await _playerStats.GetStats(scores.Select(s => s.UserId).Distinct().ToArray(), cancellationToken))
            .ToDictionary(u => u.UserId, u => u.DoublesCompetitiveLevel);
        var playerWeights = playerLevels.ToDictionary(u => u.Key, u => Math.Log(28.0 - u.Value));

        var chartTotals = scores.GroupBy(s => s.Record.ChartId)
            .ToDictionary(g => g.Key, g => g.Sum(r => playerWeights[r.UserId]));
        var entries = TierListProcessor.ProcessIntoTierList("Pass Count", chartTotals);

        var chartMinimums = scores.GroupBy(s => s.Record.ChartId)
            .ToDictionary(g => g.Key, g => g.Min(r => playerLevels[r.UserId]));

        await _tierLists.SaveEntries(entries, cancellationToken);
        foreach (var kv in chartMinimums)
            await _scoringLevels.SaveScoringLevel(MixEnum.Phoenix, kv.Key, kv.Value, cancellationToken);
    }

    private async Task ProcessPgTierList(DifficultyLevel level, ChartType chartType,
        CancellationToken cancellationToken)
    {
        var charts =
            (await _chartRepository.GetCharts(MixEnum.Phoenix, level, chartType, cancellationToken: cancellationToken))
            .ToArray();
        var pgUsers = (await _scores.GetPgUsers(chartType, level, cancellationToken)).ToArray();

        var stats = (await _playerStats.GetStats(pgUsers.Select(p => p.UserId).Distinct(), cancellationToken))
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
        await _tierLists.SaveEntries(result, cancellationToken);
    }

    private async Task ProcessPassTierList(DifficultyLevel level, ChartType chartType,
        CancellationToken cancellationToken)
    {
        var charts =
            (await _chartRepository.GetCharts(MixEnum.Phoenix, level, chartType, cancellationToken: cancellationToken))
            .ToArray();
        var userWeights = new Dictionary<int, IEnumerable<Guid>>
        {
            { 7, await _tierLists.GetUsersOnLevel(level - 3, cancellationToken, true) },
            { 6, await _tierLists.GetUsersOnLevel(level - 2, cancellationToken, true) },
            { 5, await _tierLists.GetUsersOnLevel(level - 1, cancellationToken, true) },
            { 4, await _tierLists.GetUsersOnLevel(level, cancellationToken, true) }
        };
        if (level < 27) userWeights[3] = await _tierLists.GetUsersOnLevel(level + 3, cancellationToken);
        if (level < 28) userWeights[2] = await _tierLists.GetUsersOnLevel(level + 2, cancellationToken);
        if (level < 29) userWeights[1] = await _tierLists.GetUsersOnLevel(level + 1, cancellationToken);
        var chartSums = charts.ToDictionary(c => c.Id, c => 0);
        foreach (var weightValue in userWeights)
        {
            var scores =
                (await _scores.GetScores(weightValue.Value, chartType, level, level, cancellationToken))
                .Where(s => !s.IsBroken).ToArray();

            foreach (var score in scores.Where(s => chartSums.ContainsKey(s.ChartId)))
                chartSums[score.ChartId] += weightValue.Key;
        }

        if (!chartSums.Any()) return;


        var result = new List<SongTierListEntry>();
        result.AddRange(TierListProcessor.ProcessIntoTierList("Pass Count", chartSums));
        await _tierLists.SaveEntries(result, cancellationToken);
    }
}
