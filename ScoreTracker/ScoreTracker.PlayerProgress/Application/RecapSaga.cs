using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.PlayerProgress.Contracts.Events;
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
internal sealed class RecapSaga : IConsumer<CalculateSeasonRecapsCommand>,
    IConsumer<ScoreHighlightsCapturedEvent>
{
    /// <summary>Below this many passes there isn't enough data for a meaningful recap.</summary>
    public const int MinimumPasses = 10;

    private const int MaxArcPoints = 40;

    /// <summary>The difficulty-title ladder by name — resolves a player's highest reached title.</summary>
    private static readonly IReadOnlyDictionary<Name, PhoenixDifficultyTitle> DifficultyTitles =
        PhoenixTitleList.BuildList().OfType<PhoenixDifficultyTitle>().ToDictionary(t => t.Name);

    private readonly IMemoryCache _cache;
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
        IMediator mediator, IDateTimeOffsetAccessor dateTime, IMemoryCache cache, ILogger<RecapSaga> logger)
    {
        _cache = cache;
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

    /// <summary>
    ///     Recap freshness rides the same signal as the Discord card: a settled score
    ///     batch. Cross-player inputs come from the 30-minute shared cache so a busy
    ///     import evening stays cheap — only the triggering player's own data is re-read.
    /// </summary>
    public async Task Consume(ConsumeContext<ScoreHighlightsCapturedEvent> context)
    {
        if (context.Message.Mix != MixEnum.Phoenix) return;
        try
        {
            var shared = await GetSharedCached(MixEnum.Phoenix, context.CancellationToken);
            await ComputeAndSave(MixEnum.Phoenix, context.Message.UserId, shared, context.CancellationToken);
        }
        catch (Exception e)
        {
            // Recaps are a nicety — a failure here must never disturb the import pipeline.
            _logger.LogError(e, "Post-session recap recompute failed for {UserId}", context.Message.UserId);
        }
    }

    public async Task Consume(ConsumeContext<CalculateSeasonRecapsCommand> context)
    {
        var mix = context.Message.Mix;
        var cancellationToken = context.CancellationToken;
        // Admin-triggered runs load fresh and reseed the shared cache the event path reads.
        var shared = await LoadShared(mix, cancellationToken);
        _cache.Set(SharedCacheKey(mix), shared, TimeSpan.FromMinutes(30));

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
            await BuildImpressiveScores(mix, userId, myStats, shared, cancellationToken),
            BuildRarestPasses(passes, myStats, shared),
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

        return new RecapRollup(playDays, bests.Min(b => b.RecordedDate), passes.Length, clearRank,
            clearPercentile, singlesRank, singlesPercentile, doublesRank, doublesPercentile,
            totalSeconds, totalNotes, topStepArtists);
    }

    /// <summary>
    ///     Passes few others on the site have. The denominator is EVERY active PIUScores
    ///     player, not players who recorded the chart — on a boss chart the only people
    ///     who record it are exactly the peers who can pass it, which read as "45% pass
    ///     it" for a chart 3% of the site has passed. Singles/doubles only count at your
    ///     competitive level − 6 or above — without that floor the list is S2s the
    ///     elites never bothered recording. Co-op is exempt (its "level" is a player
    ///     count, and rounding up four players for Gargoyle IS the rarity — 28 passers
    ///     sitewide). PassCount > 0 only guards the scoreless-pass edge.
    /// </summary>
    private IReadOnlyList<RecapRareChart> BuildRarestPasses(RecordedPhoenixScore[] passes,
        PlayerStatsRecord myStats, SharedInputs shared)
    {
        var population = Math.Max(1, shared.ActiveUserIds.Count);

        double LevelFloor(Chart chart)
        {
            var competitive = chart.Type == ChartType.Single
                ? myStats.SinglesCompetitiveLevel
                : myStats.DoublesCompetitiveLevel;
            if (competitive <= 0) competitive = myStats.CompetitiveLevel;
            return competitive - 6;
        }

        return passes
            .Where(p => shared.Charts.ContainsKey(p.ChartId))
            .Select(p => (Chart: shared.Charts[p.ChartId],
                Aggregate: shared.ChartAggregates.GetValueOrDefault(p.ChartId)))
            .Where(x => x.Aggregate is { PassCount: > 0 })
            .Where(x => x.Chart.Type == ChartType.CoOp || (int)x.Chart.Level >= LevelFloor(x.Chart))
            .OrderBy(x => x.Aggregate!.PassCount)
            .ThenByDescending(x => x.Aggregate!.Count)
            .Take(5)
            .Select(x => new RecapRareChart(x.Chart.Id, x.Chart.Song.Name.ToString(), x.Chart.Type,
                x.Chart.Level, x.Aggregate!.PassCount / (double)population, x.Aggregate.PassCount))
            .ToArray();
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

    /// <summary>
    ///     Your top-50 PGs by level form the pool; within it, the rarest win — fewest
    ///     PG holders sitewide, shown against the whole active population (round four:
    ///     the tier-list Hard-or-higher filter read badly in the wild).
    /// </summary>
    private IReadOnlyList<RecapRareChart> BuildImpressivePgs(RecordedPhoenixScore[] passes,
        SharedInputs shared)
    {
        var population = Math.Max(1, shared.ActiveUserIds.Count);
        return passes
            .Where(p => p.Plate == PhoenixPlate.PerfectGame && shared.Charts.ContainsKey(p.ChartId))
            .Select(p => shared.Charts[p.ChartId])
            .OrderByDescending(c => (int)c.Level)
            .Take(50)
            .Select(c => (Chart: c, Aggregate: shared.ChartAggregates.GetValueOrDefault(c.Id)))
            .Where(x => x.Aggregate is { PgCount: > 0 })
            .OrderBy(x => x.Aggregate!.PgCount)
            .ThenByDescending(x => (int)x.Chart.Level)
            .Take(5)
            .Select(x => new RecapRareChart(x.Chart.Id, x.Chart.Song.Name.ToString(), x.Chart.Type,
                x.Chart.Level, x.Aggregate!.PgCount / (double)population, x.Aggregate.PgCount))
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
        // World must be excluded by NAME: in production data it carries IsRegional = 0,
        // so trusting the flag folds all 567 World members into the "your communities"
        // tier and rivals degenerate to the global pool (2026-07-10 debugging session).
        var communityMembers = await MemberUnion(
            communities.Where(c => !c.IsRegional && c.CommunityName != "World"), cancellationToken);
        var countryMembers = await MemberUnion(
            communities.Where(c => c.IsRegional && c.CommunityName != "World"), cancellationToken);

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
        var eligible = shared.AllStats
            .Where(s => s.UserId != userId && levelByUser[s.UserId] > 0)
            .Where(s => shared.Users.TryGetValue(s.UserId, out var u) && u.IsPublic)
            .Select(s => s.UserId)
            .ToArray();

        bool InRange(Guid id, double range)
        {
            return Math.Abs(levelByUser[id] - myLevel) <= range;
        }

        // Strict priority: any member of your user-created communities inside the WIDER
        // community range outranks everyone outside them; the country community and then
        // the global pool (tight range) only top up leftover slots.
        var tiers = new[]
        {
            eligible.Where(id => communityMembers.Contains(id) &&
                                 InRange(id, RivalMatcher.CommunityCompetitiveRange)).ToArray(),
            eligible.Where(id => countryMembers.Contains(id) && !communityMembers.Contains(id) &&
                                 InRange(id, RivalMatcher.CompetitiveRange)).ToArray(),
            eligible.Where(id => !communityMembers.Contains(id) && !countryMembers.Contains(id) &&
                                 InRange(id, RivalMatcher.CompetitiveRange)).ToArray()
        };

        var myTop50 = await GetTop50Set(mix, userId, type, refresh: true, cancellationToken);
        var picked = new List<RecapRival>();
        foreach (var tier in tiers)
        {
            if (picked.Count >= 3) break;
            var candidates = new List<RivalMatcher.Candidate>();
            foreach (var candidateId in tier)
                candidates.Add(new RivalMatcher.Candidate(candidateId, levelByUser[candidateId],
                    await GetTop50Set(mix, candidateId, type, refresh: false, cancellationToken)));
            picked.AddRange(RivalMatcher.PickRivals(myTop50, myLevel, candidates, 3 - picked.Count)
                .Select(r => new RecapRival(r.Candidate.UserId,
                    shared.UserNames.GetValueOrDefault(r.Candidate.UserId, "another player"),
                    r.Candidate.CompetitiveLevel, r.Overlap)));
        }

        return picked;
    }

    private async Task<IReadOnlySet<Guid>> MemberUnion(IEnumerable<CommunityOverviewRecord> communities,
        CancellationToken cancellationToken)
    {
        var union = new HashSet<Guid>();
        foreach (var community in communities)
        {
            var members = await _cache.GetOrCreateAsync(
                $"{nameof(RecapSaga)}__Members__{community.CommunityName}",
                async o =>
                {
                    o.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                    return (await _communities.GetMembers(community.CommunityName, cancellationToken))
                        .ToArray();
                }) ?? Array.Empty<Guid>();
            union.UnionWith(members);
        }

        return union;
    }

    private async Task<IReadOnlySet<Guid>> GetTop50Set(MixEnum mix, Guid userId, ChartType type, bool refresh,
        CancellationToken cancellationToken)
    {
        // Candidate sets tolerate staleness (top-50 chart-id sets drift slowly); the
        // subject's own set always recomputes so a fresh import reshapes their overlaps.
        var key = $"{nameof(RecapSaga)}__Top50__{mix}__{type}__{userId}";
        if (!refresh && _cache.TryGetValue(key, out IReadOnlySet<Guid>? cached) && cached != null)
            return cached;

        var set = (await _mediator.Send(new GetTop50CompetitiveQuery(userId, type, mix), cancellationToken))
            .Select(s => s.ChartId)
            .ToHashSet();
        _cache.Set(key, (IReadOnlySet<Guid>)set, TimeSpan.FromHours(6));
        return set;
    }

    private async Task<SharedInputs> GetSharedCached(MixEnum mix, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(SharedCacheKey(mix), async o =>
        {
            o.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            return await LoadShared(mix, cancellationToken);
        }) ?? throw new InvalidOperationException("Recap shared inputs could not be loaded");
    }

    private static string SharedCacheKey(MixEnum mix)
    {
        return $"{nameof(RecapSaga)}__Shared__{mix}";
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

        var highest = earnedTitles
            .Where(DifficultyTitles.ContainsKey)
            .Select(t => DifficultyTitles[t])
            .OrderByDescending(t => (int)t.Level)
            .ThenByDescending(t => t.RequiredRating)
            .FirstOrDefault();

        var passedCharts = passes
            .Select(p => shared.Charts.GetValueOrDefault(p.ChartId))
            .Where(c => c != null)
            .ToArray();
        return new RecapTrophies(rarest, plates,
            highest?.Name.ToString(),
            highest != null && shared.TitleHolders.TryGetValue(highest.Name, out var holders)
                ? holders
                : null,
            GradeDistribution.Calculate(passes),
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
        var chartAggregates = (await _scores.GetChartScoreAggregates(mix, cancellationToken))
            .ToDictionary(a => a.ChartId);

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
            chartAggregates,
            allStats.Where(s => s.SinglesCompetitiveLevel > 0).Select(s => s.SinglesCompetitiveLevel).ToArray(),
            allStats.Where(s => s.DoublesCompetitiveLevel > 0).Select(s => s.DoublesCompetitiveLevel).ToArray(),
            titleHolders,
            titledUsers,
            PhoenixTitleList.BuildList().Count(),
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

    private sealed record FolderCharts(ChartType Type, DifficultyLevel Level, IReadOnlyList<Guid> ChartIds);

    private sealed record SharedInputs(
        IReadOnlyDictionary<Guid, Chart> Charts,
        IReadOnlySet<Guid> ActiveUserIds,
        PlayerStatsRecord[] AllStats,
        IReadOnlyDictionary<Guid, User> Users,
        IReadOnlyDictionary<Guid, string> UserNames,
        IReadOnlyList<WeeklyPlacingRow> WeeklyRows,
        IReadOnlyDictionary<Guid, Chart> Phoenix2Charts,
        IReadOnlyDictionary<Guid, ChartScoreAggregate> ChartAggregates,
        double[] SinglesLevels,
        double[] DoublesLevels,
        IReadOnlyDictionary<Name, int> TitleHolders,
        int TitledUsers,
        int TotalTitles,
        IReadOnlySet<Guid> BanYaChartIds,
        IReadOnlySet<Guid> CoOpX2ChartIds,
        IReadOnlySet<Guid> Singles24PlusChartIds,
        Guid? UhHeungSingles22,
        IReadOnlyList<FolderCharts> Folders);
}
