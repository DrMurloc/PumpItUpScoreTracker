using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Domain.Recap;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     Computes and persists season recaps (docs/design/phoenix-season-recap.md). One bus
///     trigger computes one player, or sweeps every player with stats when UserId is null;
///     each player is failure-isolated so one bad account can't sink the sweep. All
///     cross-player inputs (stats table, title rarity, tier lists, chart sets) are loaded
///     once per consume and shared — per-player work is in-memory except the top-50 reads
///     and the bucket-cached cohort scores.
/// </summary>
internal sealed class RecapSaga : IConsumer<CalculateSeasonRecapsCommand>
{
    /// <summary>Below this many passes there isn't enough data for a meaningful recap.</summary>
    public const int MinimumPasses = 10;

    private const int MaxArcPoints = 40;

    private readonly IChartRepository _charts;
    private readonly CohortScoreProvider _cohorts;
    private readonly ICommunityReader _communities;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly ILogger<RecapSaga> _logger;
    private readonly IMediator _mediator;
    private readonly IPlayerSeasonRecapRepository _recaps;
    private readonly IScoreReader _scores;
    private readonly IPlayerStatsReader _stats;
    private readonly ITitleRepository _titles;
    private readonly IUserReader _users;
    private readonly IWeeklyPlacingReader _weekly;

    public RecapSaga(IChartRepository charts, IScoreReader scores, ITitleRepository titles,
        IPlayerStatsReader stats, IUserReader users, IPlayerSeasonRecapRepository recaps,
        CohortScoreProvider cohorts, IWeeklyPlacingReader weekly, ICommunityReader communities,
        IMediator mediator, IDateTimeOffsetAccessor dateTime, ILogger<RecapSaga> logger)
    {
        _charts = charts;
        _scores = scores;
        _titles = titles;
        _stats = stats;
        _users = users;
        _recaps = recaps;
        _cohorts = cohorts;
        _weekly = weekly;
        _communities = communities;
        _mediator = mediator;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CalculateSeasonRecapsCommand> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;
        var shared = await LoadShared(mix, cancellationToken);

        if (context.Message.UserId is { } single)
        {
            await ComputeAndSave(mix, single, shared, cancellationToken);
            return;
        }

        var userIds = shared.ActiveUserIds;
        _logger.LogInformation("Recap sweep starting for {Count} players on {Mix}", userIds.Count, mix);
        var computed = 0;
        foreach (var userId in userIds)
            try
            {
                if (await ComputeAndSave(mix, userId, shared, cancellationToken)) computed++;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Recap computation failed for {UserId} on {Mix}", userId, mix);
            }

        _logger.LogInformation("Recap sweep finished: {Computed} of {Count} players on {Mix}",
            computed, userIds.Count, mix);
    }

    private async Task<bool> ComputeAndSave(MixEnum mix, Guid userId, SharedInputs shared,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetUser(userId, cancellationToken);
        if (user == null) return false;

        var bests = (await _scores.GetBestScores(mix, userId, cancellationToken)).ToArray();
        var passes = bests.Where(b => !b.IsBroken).ToArray();
        if (passes.Length < MinimumPasses) return false;

        var myStats = await _stats.GetStats(mix, userId, cancellationToken);
        var top50Pumbility = (await _mediator.Send(
                new GetTop50ForPlayerQuery(userId, null, 50, mix), cancellationToken))
            .Where(s => s is { IsBroken: false, Score: not null })
            .Select(s => s.Score!.Value)
            .ToArray();

        var playerType = RecapPlayerTypeCalculator.Calculate(top50Pumbility);
        var recap = new PlayerRecap(
            PlayerRecap.CurrentSchemaVersion,
            _dateTime.Now,
            await BuildArc(mix, userId, cancellationToken),
            await BuildRollup(mix, userId, bests, passes, myStats, shared, cancellationToken),
            playerType,
            top50Pumbility.Length == 0 ? null : (int)Math.Round(top50Pumbility.Average(s => (int)s)),
            await BuildBadges(mix, userId, user, bests, passes, shared, cancellationToken),
            await BuildRivals(mix, userId, myStats, shared, cancellationToken),
            BuildImpressivePgs(passes, shared),
            BuildImpressivePasses(passes, shared),
            await BuildImpressiveScores(mix, userId, myStats, shared, cancellationToken),
            WeeklyRecapCalculator.Calculate(userId, shared.WeeklyRows, shared.Charts, shared.UserNames),
            await BuildTrophies(mix, userId, passes, shared, cancellationToken),
            Phoenix2ProjectionCalculator.Calculate(bests, shared.Phoenix2Charts));

        await _recaps.SaveRecap(userId, mix, recap, cancellationToken);
        return true;
    }

    private async Task<RecapArc?> BuildArc(MixEnum mix, Guid userId, CancellationToken cancellationToken)
    {
        var history = (await _mediator.Send(new GetPlayerHistoryQuery(userId, mix), cancellationToken))
            .OrderBy(h => h.Date)
            .ToArray();
        if (history.Length < 2) return null;

        var first = history[0];
        var last = history[^1];
        var points = history.Length <= MaxArcPoints
            ? history
            : Enumerable.Range(0, MaxArcPoints)
                .Select(i => history[(int)Math.Round(i * (history.Length - 1) / (double)(MaxArcPoints - 1))])
                .ToArray();
        return new RecapArc(first.CompetitiveLevel, last.CompetitiveLevel,
            first.SinglesLevel, last.SinglesLevel,
            first.DoublesLevel, last.DoublesLevel,
            first.PassCount, last.PassCount,
            points.Select(p => new RecapArcPoint(p.Date, p.CompetitiveLevel)).ToArray());
    }

    private async Task<RecapRollup> BuildRollup(MixEnum mix, Guid userId, RecordedPhoenixScore[] bests,
        RecordedPhoenixScore[] passes, PlayerStatsRecord myStats, SharedInputs shared,
        CancellationToken cancellationToken)
    {
        var playDays = await _scores.GetPlayDayCount(mix, userId, cancellationToken);

        var clearRank = 1 + shared.AllStats.Count(s => s.ClearCount > myStats.ClearCount);
        var clearPercentile = clearRank / (double)Math.Max(1, shared.AllStats.Length);

        var (singlesRank, singlesPercentile) = RankAmong(shared.SinglesLevels, myStats.SinglesCompetitiveLevel);
        var (doublesRank, doublesPercentile) = RankAmong(shared.DoublesLevels, myStats.DoublesCompetitiveLevel);

        var recordedCharts = bests
            .Select(b => shared.Charts.GetValueOrDefault(b.ChartId))
            .Where(c => c != null)
            .Select(c => c!)
            .ToArray();
        var totalSeconds = (long)recordedCharts
            .Aggregate(TimeSpan.Zero, (sum, c) => sum + c.Song.Duration).TotalSeconds;
        var totalNotes = recordedCharts.Sum(c => (long)(c.NoteCount ?? 0));
        var topStepArtists = recordedCharts
            .Where(c => c.StepArtist != null && c.StepArtist.Value != "Unknown")
            .GroupBy(c => c.StepArtist!.Value)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new RecapStepArtist(g.Key.ToString(), g.Count()))
            .ToArray();

        return new RecapRollup(playDays, passes.Length, clearRank, clearPercentile,
            singlesRank, singlesPercentile, doublesRank, doublesPercentile,
            totalSeconds, totalNotes, topStepArtists);
    }

    private static (int? Rank, double? Percentile) RankAmong(double[] rankedLevels, double mine)
    {
        if (mine <= 0 || rankedLevels.Length == 0) return (null, null);
        var rank = 1 + rankedLevels.Count(l => l > mine);
        return (rank, rank / (double)rankedLevels.Length);
    }

    private async Task<IReadOnlyList<RecapEarnedBadge>> BuildBadges(MixEnum mix, Guid userId, User user,
        RecordedPhoenixScore[] bests, RecordedPhoenixScore[] passes, SharedInputs shared,
        CancellationToken cancellationToken)
    {
        var badges = new List<RecapEarnedBadge>();
        var earnedTitles = (await _titles.GetCompletedTitles(mix, userId, cancellationToken))
            .Select(t => t.Title)
            .ToArray();

        var collectionShare = shared.TotalTitles == 0 ? 0 : earnedTitles.Length / (double)shared.TotalTitles;
        if (RecapBadges.CollectionBadge(earnedTitles.Length, shared.TotalTitles) is { } collection)
            badges.Add(new RecapEarnedBadge(collection, collectionShare, earnedTitles.Length, null));

        if (RecapBadges.Snowflake(earnedTitles, shared.TitleHolders, shared.TitledUsers) is { } snowflake)
            badges.Add(new RecapEarnedBadge(RecapBadge.SpecialSnowflake, snowflake.HolderShare, null,
                snowflake.Title.ToString()));

        var passedByChart = passes.ToDictionary(p => p.ChartId);
        var folders = shared.Folders
            .Select(f => new RecapFolder(f.Type, f.Level, f.ChartIds.Count,
                f.ChartIds.Count(passedByChart.ContainsKey)))
            .ToArray();
        var foldersOver90 = RecapBadges.CountFoldersOver90(folders);
        if (RecapBadges.CompletionistBadge(foldersOver90) is { } completionist)
            badges.Add(new RecapEarnedBadge(completionist, null, foldersOver90, null));

        var coOpPassed = shared.CoOpX2ChartIds.Count(passedByChart.ContainsKey);
        if (RecapBadges.CoOpBadge(shared.CoOpX2ChartIds.Count, coOpPassed) is { } coOp)
            badges.Add(new RecapEarnedBadge(coOp,
                shared.CoOpX2ChartIds.Count == 0 ? null : coOpPassed / (double)shared.CoOpX2ChartIds.Count,
                coOpPassed, null));

        var banYaPassed = shared.BanYaChartIds.Count(passedByChart.ContainsKey);
        if (RecapBadges.BanYaBadge(shared.BanYaChartIds.Count, banYaPassed) is { } banYa)
            badges.Add(new RecapEarnedBadge(banYa,
                shared.BanYaChartIds.Count == 0 ? null : banYaPassed / (double)shared.BanYaChartIds.Count,
                banYaPassed, null));

        if (shared.UhHeungSingles22 is { } uhHeung &&
            RecapBadges.EarnsBigFeet(bests.FirstOrDefault(b => b.ChartId == uhHeung)))
            badges.Add(new RecapEarnedBadge(RecapBadge.BigFeetOrInjuredBack, null, null, "Uh-Heung S22"));

        var s24Records = bests.Where(b => shared.Singles24PlusChartIds.Contains(b.ChartId)).ToArray();
        if (RecapBadges.EarnsGrandMashter(s24Records, shared.Singles24PlusChartIds.Count))
            badges.Add(new RecapEarnedBadge(RecapBadge.GrandMashter, null, null, null));

        var passedDoublesLevels = passes
            .Select(p => shared.Charts.GetValueOrDefault(p.ChartId))
            .Where(c => c is { Type: ChartType.Double })
            .Select(c => c!.Level);
        if (RecapBadges.EarnsNowYouCanPlayTheGame(passedDoublesLevels))
            badges.Add(new RecapEarnedBadge(RecapBadge.NowYouCanPlayTheGame, null, null, null));

        if (RecapBadges.EarnsDove(user.GameTag?.ToString()))
            badges.Add(new RecapEarnedBadge(RecapBadge.Dove, null, null, null));

        return badges;
    }

    private IReadOnlyList<RecapChartHighlight> BuildImpressivePgs(RecordedPhoenixScore[] passes,
        SharedInputs shared)
    {
        return passes
            .Where(p => p.Plate == PhoenixPlate.PerfectGame)
            .Select(p => (Record: p, Chart: shared.Charts.GetValueOrDefault(p.ChartId)))
            .Where(x => x.Chart != null)
            .Select(x => (x.Chart!, Tier: shared.PgTiers.GetValueOrDefault(x.Chart!.Id, TierListCategory.Unrecorded)))
            .Where(x => IsHardOrHigher(x.Tier))
            .OrderByDescending(x => (int)x.Item1.Level)
            .ThenByDescending(x => x.Tier)
            .Take(6)
            .Select(x => new RecapChartHighlight(x.Item1.Id, x.Item1.Song.Name.ToString(), x.Item1.Type,
                x.Item1.Level, x.Tier))
            .ToArray();
    }

    private IReadOnlyList<RecapChartHighlight> BuildImpressivePasses(RecordedPhoenixScore[] passes,
        SharedInputs shared)
    {
        var candidates = passes
            .Select(p => shared.Charts.GetValueOrDefault(p.ChartId))
            .Where(c => c != null)
            .Select(c => (Chart: c!, Tier: shared.PassTiers.GetValueOrDefault(c!.Id, TierListCategory.Unrecorded)))
            .Where(x => IsHardOrHigher(x.Tier))
            .ToArray();
        return new[] { ChartType.Single, ChartType.Double }
            .SelectMany(type => candidates
                .Where(x => x.Chart.Type == type)
                .OrderByDescending(x => (int)x.Chart.Level)
                .ThenByDescending(x => x.Tier)
                .Take(3))
            .Select(x => new RecapChartHighlight(x.Chart.Id, x.Chart.Song.Name.ToString(), x.Chart.Type,
                x.Chart.Level, x.Tier))
            .ToArray();
    }

    private async Task<IReadOnlyList<RecapScoreHighlight>> BuildImpressiveScores(MixEnum mix, Guid userId,
        PlayerStatsRecord myStats, SharedInputs shared, CancellationToken cancellationToken)
    {
        var result = new List<RecapScoreHighlight>();
        foreach (var type in new[] { ChartType.Single, ChartType.Double })
        {
            var level = type == ChartType.Single
                ? myStats.SinglesCompetitiveLevel
                : myStats.DoublesCompetitiveLevel;
            if (level <= 0) continue;

            var candidates = (await _mediator.Send(new GetTop50CompetitiveQuery(userId, type, mix),
                    cancellationToken))
                .Where(s => s is { IsBroken: false, Score: not null } && s.Plate != PhoenixPlate.PerfectGame)
                .Where(s => shared.Charts.ContainsKey(s.ChartId))
                .ToArray();
            if (candidates.Length == 0) continue;

            var cohortScores = await _cohorts.GetCohortScoresByChart(mix, type,
                CohortScoreProvider.Bucket(level),
                candidates.Select(c => c.ChartId).ToHashSet(), cancellationToken);

            // Candidates arrive fung-descending (most competitive-contributing first);
            // walk down until three clear the >90%-vs-peers bar.
            foreach (var candidate in candidates)
            {
                var percentile = cohortScores.TryGetValue(candidate.ChartId, out var cohort)
                    ? ScoreRankings.TieInclusivePercentile(cohort, candidate.Score!.Value)
                    : 1.0;
                if (percentile <= 0.9) continue;

                var chart = shared.Charts[candidate.ChartId];
                result.Add(new RecapScoreHighlight(chart.Id, chart.Song.Name.ToString(), chart.Type,
                    chart.Level, candidate.Score!.Value, percentile));
                if (result.Count(r => r.ChartType == type) >= 3) break;
            }
        }

        return result;
    }

    private async Task<RecapRivals?> BuildRivals(MixEnum mix, Guid userId, PlayerStatsRecord myStats,
        SharedInputs shared, CancellationToken cancellationToken)
    {
        var communities = (await _communities.GetUserCommunities(userId, cancellationToken)).ToArray();
        var communityMembers = await MemberUnion(communities.Where(c => !c.IsRegional), shared, cancellationToken);
        // The regional system communities are World + one per country; excluding World
        // leaves exactly the player's country community (owner call: country beats
        // global but user-created communities beat both).
        var countryMembers = await MemberUnion(
            communities.Where(c => c.IsRegional && c.CommunityName != "World"), shared, cancellationToken);

        var singles = await PickRivalsForType(mix, userId, ChartType.Single, myStats.SinglesCompetitiveLevel,
            communityMembers, countryMembers, shared, cancellationToken);
        var doubles = await PickRivalsForType(mix, userId, ChartType.Double, myStats.DoublesCompetitiveLevel,
            communityMembers, countryMembers, shared, cancellationToken);
        if (singles.Count == 0 && doubles.Count == 0) return null;

        return new RecapRivals(singles, doubles);
    }

    private async Task<IReadOnlyList<RecapRival>> PickRivalsForType(MixEnum mix, Guid userId, ChartType type,
        double myLevel, IReadOnlySet<Guid> communityMembers, IReadOnlySet<Guid> countryMembers,
        SharedInputs shared, CancellationToken cancellationToken)
    {
        if (myLevel <= 0) return Array.Empty<RecapRival>();

        var levelByUser = shared.AllStats.ToDictionary(s => s.UserId,
            s => type == ChartType.Single ? s.SinglesCompetitiveLevel : s.DoublesCompetitiveLevel);
        var inRange = shared.AllStats
            .Where(s => s.UserId != userId)
            .Where(s => shared.Users.TryGetValue(s.UserId, out var u) && u.IsPublic)
            .Where(s => Math.Abs(levelByUser[s.UserId] - myLevel) <= RivalMatcher.CompetitiveRange &&
                        levelByUser[s.UserId] > 0)
            .Select(s => s.UserId)
            .ToArray();

        var pool = RivalMatcher.SelectPool(
            inRange.Where(communityMembers.Contains).ToArray(),
            inRange.Where(countryMembers.Contains).ToArray(),
            inRange);
        if (pool.Count == 0) return Array.Empty<RecapRival>();

        var myTop50 = await GetTop50Set(mix, userId, type, shared, cancellationToken);
        var candidates = new List<RivalMatcher.Candidate>();
        foreach (var candidateId in pool)
            candidates.Add(new RivalMatcher.Candidate(candidateId, levelByUser[candidateId],
                await GetTop50Set(mix, candidateId, type, shared, cancellationToken)));

        return RivalMatcher.PickRivals(myTop50, myLevel, candidates)
            .Select(r => new RecapRival(r.Candidate.UserId,
                shared.UserNames.GetValueOrDefault(r.Candidate.UserId, "another player"),
                r.Candidate.CompetitiveLevel, r.Overlap))
            .ToArray();
    }

    private async Task<IReadOnlySet<Guid>> MemberUnion(IEnumerable<CommunityOverviewRecord> communities,
        SharedInputs shared, CancellationToken cancellationToken)
    {
        var union = new HashSet<Guid>();
        foreach (var community in communities)
        {
            var key = community.CommunityName.ToString();
            if (!shared.CommunityMembers.TryGetValue(key, out var members))
            {
                members = (await _communities.GetMembers(community.CommunityName, cancellationToken)).ToArray();
                shared.CommunityMembers[key] = members;
            }

            union.UnionWith(members);
        }

        return union;
    }

    private async Task<IReadOnlySet<Guid>> GetTop50Set(MixEnum mix, Guid userId, ChartType type,
        SharedInputs shared, CancellationToken cancellationToken)
    {
        if (shared.Top50Sets.TryGetValue((userId, type), out var cached)) return cached;

        var set = (await _mediator.Send(new GetTop50CompetitiveQuery(userId, type, mix), cancellationToken))
            .Select(s => s.ChartId)
            .ToHashSet();
        shared.Top50Sets[(userId, type)] = set;
        return set;
    }

    private async Task<RecapTrophies> BuildTrophies(MixEnum mix, Guid userId, RecordedPhoenixScore[] passes,
        SharedInputs shared, CancellationToken cancellationToken)
    {
        var earnedTitles = (await _titles.GetCompletedTitles(mix, userId, cancellationToken))
            .Select(t => t.Title)
            .ToArray();
        var rarest = RecapBadges.RarestTitles(earnedTitles, shared.TitleHolders, shared.TitledUsers, 3)
            .Select(t => new RecapRareTitle(t.Title.ToString(), t.HolderShare))
            .ToArray();
        var plates = RecapBadges.PlateCabinet(passes)
            .OrderByDescending(kv => kv.Key)
            .Select(kv => new RecapPlateCount(kv.Key, kv.Value))
            .ToArray();

        var oldest = passes
            .Where(p => shared.Charts.ContainsKey(p.ChartId))
            .OrderBy(p => p.RecordedDate)
            .Select(p => (Record: p, Chart: shared.Charts[p.ChartId]))
            .Select(x => new RecapOldestBest(x.Chart.Id, x.Chart.Song.Name.ToString(), x.Chart.Type,
                x.Chart.Level, x.Record.Score == null ? null : (int)x.Record.Score.Value, x.Record.RecordedDate))
            .FirstOrDefault();

        var passedCharts = passes
            .Select(p => shared.Charts.GetValueOrDefault(p.ChartId))
            .Where(c => c != null)
            .ToArray();
        return new RecapTrophies(rarest, plates, oldest,
            passedCharts.Count(c => c!.Type == ChartType.Single),
            passedCharts.Count(c => c!.Type == ChartType.Double));
    }

    private async Task<SharedInputs> LoadShared(MixEnum mix, CancellationToken cancellationToken)
    {
        var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        // Everyone with any recorded best on the mix — the sweep list AND the ranking
        // population ("all players with PIUScores Phoenix data").
        var activeUserIds = await _scores.GetActiveUserIds(mix, DateTimeOffset.MinValue, cancellationToken);
        var allStats = (await _stats.GetStats(mix, activeUserIds, cancellationToken)).ToArray();
        var users = (await _users.GetUsers(activeUserIds, cancellationToken)).ToDictionary(u => u.Id);
        var weeklyRows = (await _weekly.GetAllPlacings(mix, cancellationToken)).ToArray();
        // The "next season" finale only makes sense projecting Phoenix onto Phoenix 2.
        var phoenix2Charts = mix == MixEnum.Phoenix
            ? (await _charts.GetCharts(MixEnum.Phoenix2, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id)
            : new Dictionary<Guid, Chart>();
        var titleHolders = (await _titles.GetTitleAggregations(mix, cancellationToken))
            .GroupBy(t => t.Title)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Count));
        var titledUsers = await _titles.CountTitledUsers(cancellationToken);
        var pgTiers = await LoadTierCategories("PG", mix, cancellationToken);
        var passTiers = await LoadTierCategories("Difficulty", mix, cancellationToken);

        var folders = charts.Values
            .Where(c => c.Type is ChartType.Single or ChartType.Double)
            .GroupBy(c => (c.Type, Level: (int)c.Level))
            .Select(g => new FolderCharts(g.Key.Type, g.Key.Level, g.Select(c => c.Id).ToArray()))
            .ToArray();

        return new SharedInputs(
            charts,
            activeUserIds,
            allStats,
            users,
            users.Values.ToDictionary(u => u.Id, u => u.Name.ToString()),
            weeklyRows,
            phoenix2Charts,
            allStats.Where(s => s.SinglesCompetitiveLevel > 0).Select(s => s.SinglesCompetitiveLevel).ToArray(),
            allStats.Where(s => s.DoublesCompetitiveLevel > 0).Select(s => s.DoublesCompetitiveLevel).ToArray(),
            titleHolders,
            titledUsers,
            PhoenixTitleList.BuildList().Count(),
            pgTiers,
            passTiers,
            charts.Values.Where(c => RecapBadges.IsBanYaArtist(c.Song.Artist)).Select(c => c.Id).ToHashSet(),
            charts.Values.Where(c => c.Type == ChartType.CoOp && (int)c.Level == 2).Select(c => c.Id)
                .ToHashSet(),
            charts.Values.Where(c => c.Type == ChartType.Single && (int)c.Level >= 24).Select(c => c.Id)
                .ToHashSet(),
            charts.Values.FirstOrDefault(c =>
                c.Type == ChartType.Single && (int)c.Level == 22 &&
                c.Song.Name.ToString().Contains("Uh-Heung", StringComparison.OrdinalIgnoreCase))?.Id,
            folders);
    }

    private async Task<IReadOnlyDictionary<Guid, TierListCategory>> LoadTierCategories(Name tierListName,
        MixEnum mix, CancellationToken cancellationToken)
    {
        return (await _mediator.Send(new GetTierListQuery(tierListName, mix), cancellationToken))
            .GroupBy(e => e.ChartId)
            .ToDictionary(g => g.Key, g => g.First().Category);
    }

    private static bool IsHardOrHigher(TierListCategory category)
    {
        return category is TierListCategory.Hard or TierListCategory.VeryHard or TierListCategory.Underrated;
    }

    private sealed record FolderCharts(ChartType Type, DifficultyLevel Level, IReadOnlyList<Guid> ChartIds);

    private sealed record SharedInputs(
        IReadOnlyDictionary<Guid, Chart> Charts,
        IReadOnlySet<Guid> ActiveUserIds,
        PlayerStatsRecord[] AllStats,
        IReadOnlyDictionary<Guid, User> Users,
        IReadOnlyDictionary<Guid, string> UserNames,
        IReadOnlyList<WeeklyPlacingRow> WeeklyRows,
        IReadOnlyDictionary<Guid, Chart> Phoenix2Charts,
        double[] SinglesLevels,
        double[] DoublesLevels,
        IReadOnlyDictionary<Name, int> TitleHolders,
        int TitledUsers,
        int TotalTitles,
        IReadOnlyDictionary<Guid, TierListCategory> PgTiers,
        IReadOnlyDictionary<Guid, TierListCategory> PassTiers,
        IReadOnlySet<Guid> BanYaChartIds,
        IReadOnlySet<Guid> CoOpX2ChartIds,
        IReadOnlySet<Guid> Singles24PlusChartIds,
        Guid? UhHeungSingles22,
        IReadOnlyList<FolderCharts> Folders)
    {
        /// <summary>Per-consume memo: community name → member ids (rival pools).</summary>
        public Dictionary<string, Guid[]> CommunityMembers { get; } = new();

        /// <summary>Per-consume memo: top-50 competitive chart-id sets (rival overlap).</summary>
        public Dictionary<(Guid UserId, ChartType Type), IReadOnlySet<Guid>> Top50Sets { get; } = new();
    }
}
