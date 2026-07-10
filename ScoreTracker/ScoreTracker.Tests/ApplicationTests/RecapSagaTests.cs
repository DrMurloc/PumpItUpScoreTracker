using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RecapSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task PlayersWithFewerThanTenPassesGetNoRecap()
    {
        var ctx = new HandlerContext();
        ctx.GivenEligiblePasses(9);

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        ctx.Recaps.Verify(r => r.SaveRecap(It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<PlayerRecap>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SavedRecapCarriesSchemaVersionAndComputedAt()
    {
        var ctx = new HandlerContext();
        ctx.GivenEligiblePasses(10);

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        Assert.NotNull(ctx.Saved);
        Assert.Equal(PlayerRecap.CurrentSchemaVersion, ctx.Saved!.SchemaVersion);
        Assert.Equal(Now, ctx.Saved.ComputedAt);
    }

    [Fact]
    public async Task SweepComputesEveryActiveUserAndSurvivesFailures()
    {
        var second = Guid.NewGuid();
        var broken = Guid.NewGuid();
        var ctx = new HandlerContext(activeUsers: new[] { UserId, second, broken });
        ctx.GivenEligiblePasses(10, forAnyUser: true);
        ctx.Users.Setup(u => u.GetUser(broken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bad account"));

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(null)));

        ctx.Recaps.Verify(r => r.SaveRecap(UserId, MixEnum.Phoenix, It.IsAny<PlayerRecap>(),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Recaps.Verify(r => r.SaveRecap(second, MixEnum.Phoenix, It.IsAny<PlayerRecap>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlayerTypeComesFromTheTopFiftyPumbilityAverage()
    {
        var ctx = new HandlerContext();
        ctx.GivenEligiblePasses(10);
        ctx.GivenTop50Pumbility(Enumerable.Repeat(972_000, 10).ToArray());

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        Assert.Equal(RecapPlayerType.BalancedPlayer, ctx.Saved!.PlayerType);
        Assert.Equal(972_000, ctx.Saved.PlayerTypeAverageScore);
    }

    [Fact]
    public async Task TitleLadderBadgeCarriesShareAndCount()
    {
        var ctx = new HandlerContext();
        ctx.GivenEligiblePasses(10);
        // 108 of the 213 Phoenix titles is just past the strict 50% Hunter bar.
        ctx.GivenCompletedTitles(Enumerable.Range(0, 108).Select(i => $"Title {i}").ToArray());

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        var badge = ctx.Saved!.Badges.Single(b => b.Badge == RecapBadge.TitleHunter);
        Assert.Equal(108, badge.Count);
        Assert.NotNull(badge.Share);
    }

    [Fact]
    public async Task SnowflakeBadgeNamesTheRarestTitle()
    {
        var ctx = new HandlerContext();
        ctx.GivenEligiblePasses(10);
        ctx.GivenCompletedTitles("Rare Title");
        ctx.GivenTitleRarity(("Rare Title", 5), ("Common Title", 800));

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        var badge = ctx.Saved!.Badges.Single(b => b.Badge == RecapBadge.SpecialSnowflake);
        Assert.Equal("Rare Title", badge.Subject);
        Assert.Equal(.005, badge.Share!.Value, 5);
    }

    [Fact]
    public async Task BigFeetRidesTheUhHeungSingles22Chart()
    {
        var uhHeung = new ChartBuilder().WithSongName("Uh-Heung").WithType(ChartType.Single).WithLevel(22).Build();
        var ctx = new HandlerContext(uhHeung);
        ctx.GivenEligiblePasses(10);
        ctx.GivenPass(uhHeung, 996_000);

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        Assert.Contains(ctx.Saved!.Badges, b => b.Badge == RecapBadge.BigFeetOrInjuredBack);
    }

    [Fact]
    public async Task DoveIsGrantedByExactGameTag()
    {
        var ctx = new HandlerContext(gameTag: "DULKI #2827");
        ctx.GivenEligiblePasses(10);

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        Assert.Contains(ctx.Saved!.Badges, b => b.Badge == RecapBadge.Dove);
    }

    [Fact]
    public async Task ImpressivePassesOnlyIncludeHardOrHigherTierCharts()
    {
        var hard = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).WithSongName("Hard Song").Build();
        var medium = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).WithSongName("Medium Song").Build();
        var ctx = new HandlerContext(hard, medium);
        ctx.GivenEligiblePasses(8);
        ctx.GivenPass(hard, 950_000);
        ctx.GivenPass(medium, 950_000);
        ctx.GivenTierList("Difficulty", (hard.Id, TierListCategory.Hard), (medium.Id, TierListCategory.Medium));

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        var highlight = Assert.Single(ctx.Saved!.ImpressivePasses);
        Assert.Equal(hard.Id, highlight.ChartId);
        Assert.Equal(TierListCategory.Hard, highlight.Difficulty);
    }

    [Fact]
    public async Task ImpressivePgsOrderByFolderThenTier()
    {
        var s16 = new ChartBuilder().WithType(ChartType.Single).WithLevel(16).WithSongName("Small PG").Build();
        var s18 = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).WithSongName("Big PG").Build();
        var ctx = new HandlerContext(s16, s18);
        ctx.GivenEligiblePasses(8);
        ctx.GivenPass(s16, 1_000_000, PhoenixPlate.PerfectGame);
        ctx.GivenPass(s18, 1_000_000, PhoenixPlate.PerfectGame);
        ctx.GivenTierList("PG", (s16.Id, TierListCategory.VeryHard), (s18.Id, TierListCategory.Hard));

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        Assert.Equal(new[] { s18.Id, s16.Id }, ctx.Saved!.ImpressivePgs.Select(p => p.ChartId).ToArray());
    }

    [Fact]
    public async Task ImpressiveScoresSkipPgsAndRequireNinetyPercentVsPeers()
    {
        var pgChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).WithSongName("PG Chart").Build();
        var strong = new ChartBuilder().WithType(ChartType.Single).WithLevel(19).WithSongName("Strong Score").Build();
        var weak = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).WithSongName("Weak Score").Build();
        var ctx = new HandlerContext(pgChart, strong, weak);
        ctx.GivenEligiblePasses(10);
        ctx.GivenTop50Competitive(ChartType.Single,
            (pgChart, 1_000_000, PhoenixPlate.PerfectGame),
            (strong, 985_000, PhoenixPlate.FairGame),
            (weak, 960_000, PhoenixPlate.FairGame));
        ctx.GivenCohortScores(strong, 950_000, 960_000, 970_000);
        ctx.GivenCohortScores(weak, 970_000, 980_000, 990_000);

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        var highlight = Assert.Single(ctx.Saved!.ImpressiveScores);
        Assert.Equal(strong.Id, highlight.ChartId);
        Assert.Equal(985_000, highlight.Score);
        Assert.True(highlight.PercentileVsPeers > .9);
    }

    [Fact]
    public async Task RollupRanksAgainstAllActivePlayers()
    {
        var better = Guid.NewGuid();
        var worse = Guid.NewGuid();
        var ctx = new HandlerContext(activeUsers: new[] { UserId, better, worse });
        ctx.GivenEligiblePasses(10);
        ctx.GivenAllStats(
            Stats(UserId, clearCount: 100, singles: 20.0),
            Stats(better, clearCount: 150, singles: 22.0),
            Stats(worse, clearCount: 50, singles: 18.0));

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        Assert.Equal(2, ctx.Saved!.Rollup.ChartsPassedRank);
        Assert.Equal(2, ctx.Saved.Rollup.SinglesRank);
        Assert.Equal(2 / 3.0, ctx.Saved.Rollup.SinglesPercentile!.Value, 5);
    }

    [Fact]
    public async Task ArcSpansFirstToLatestHistorySnapshot()
    {
        var ctx = new HandlerContext();
        ctx.GivenEligiblePasses(10);
        ctx.GivenHistory(
            new PlayerRatingRecord(UserId, Now.AddYears(-3), 17.8, 17.8, 16.0, 0, 400),
            new PlayerRatingRecord(UserId, Now.AddYears(-1), 20.1, 20.1, 18.5, 0, 1200),
            new PlayerRatingRecord(UserId, Now, 21.4, 21.4, 20.6, 0, 1847));

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        Assert.NotNull(ctx.Saved!.Arc);
        Assert.Equal(17.8, ctx.Saved.Arc!.StartCompetitive);
        Assert.Equal(21.4, ctx.Saved.Arc.EndCompetitive);
        Assert.Equal(1847, ctx.Saved.Arc.EndPassCount);
        Assert.Equal(3, ctx.Saved.Arc.Points.Count);
    }

    [Fact]
    public async Task PlayDaysComeFromTheJournal()
    {
        var ctx = new HandlerContext();
        ctx.GivenEligiblePasses(10);
        ctx.Scores.Setup(s => s.GetPlayDayCount(MixEnum.Phoenix, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(214);

        await ctx.Saga.Consume(ctx.Context(new CalculateSeasonRecapsCommand(UserId)));

        Assert.Equal(214, ctx.Saved!.Rollup.PlayDays);
    }

    private static PlayerStatsRecord Stats(Guid userId, int clearCount, double singles)
    {
        return new PlayerStatsRecord(userId, 0, 1, clearCount, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1,
            singles, singles, 0);
    }

    private sealed class HandlerContext
    {
        private readonly List<RecordedPhoenixScore> _bests = new();
        private readonly List<Chart> _charts = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<ITitleRepository> Titles { get; } = new();
        public Mock<IPlayerStatsReader> PlayerStats { get; } = new();
        public Mock<IUserReader> Users { get; } = new();
        public Mock<IPlayerSeasonRecapRepository> Recaps { get; } = new();
        public Mock<IWeeklyPlacingReader> Weekly { get; } = new();
        public Mock<ICommunityReader> Communities { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public RecapSaga Saga { get; }
        public PlayerRecap? Saved { get; private set; }

        public HandlerContext(params Chart[] charts) : this(null, null, charts)
        {
        }

        public HandlerContext(Guid[]? activeUsers = null, string? gameTag = null, params Chart[] charts)
        {
            _charts.AddRange(charts);
            Charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_charts);
            Scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_bests);
            Scores.Setup(s => s.GetActiveUserIds(It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((activeUsers ?? new[] { UserId }).ToHashSet());
            Scores.Setup(s => s.GetPlayDayCount(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);
            Scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<UserPhoenixScore>());
            Titles.Setup(t => t.GetCompletedTitles(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<TitleAchievedRecord>());
            Titles.Setup(t => t.GetTitleAggregations(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<TitleAggregationRecord>());
            Titles.Setup(t => t.CountTitledUsers(It.IsAny<CancellationToken>())).ReturnsAsync(1000);
            PlayerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Stats(UserId, 100, 20.0));
            PlayerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Stats(UserId, 100, 20.0) });
            PlayerStats.Setup(p => p.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType?>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            Users.Setup(u => u.GetUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => new User(id, Name.From("Player"), true,
                    gameTag == null ? (Name?)null : Name.From(gameTag),
                    new Uri("https://example.invalid/avatar.png"), null));
            Users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => ids
                    .Select(id => new User(id, Name.From("Player"), true, null,
                        new Uri("https://example.invalid/avatar.png"), null))
                    .ToArray());
            Weekly.Setup(w => w.GetAllPlacings(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<WeeklyPlacingRow>());
            Communities.Setup(c => c.GetUserCommunities(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
            Communities.Setup(c => c.GetMembers(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());
            Recaps.Setup(r => r.SaveRecap(It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<PlayerRecap>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Guid, MixEnum, PlayerRecap, CancellationToken>((_, _, recap, _) => Saved = recap)
                .Returns(Task.CompletedTask);
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50CompetitiveQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<GetTierListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SongTierListEntry>());
            Mediator.Setup(m => m.Send(It.IsAny<GetPlayerHistoryQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<PlayerRatingRecord>());

            var cache = new MemoryCache(new MemoryCacheOptions());
            Saga = new RecapSaga(Charts.Object, Scores.Object, Titles.Object, PlayerStats.Object,
                Users.Object, Recaps.Object,
                new CohortScoreProvider(PlayerStats.Object, Scores.Object, cache),
                Weekly.Object, Communities.Object,
                Mediator.Object, FakeDateTime.At(Now).Object, NullLogger<RecapSaga>.Instance);
        }

        public void GivenEligiblePasses(int count, bool forAnyUser = false)
        {
            for (var i = 0; i < count; i++)
            {
                var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(15)
                    .WithSongName($"Filler {i}").Build();
                _charts.Add(chart);
                _bests.Add(new RecordedPhoenixScore(chart.Id, 920_000, PhoenixPlate.FairGame, false, Now));
            }

            if (forAnyUser)
                Scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(_bests);
        }

        public void GivenPass(Chart chart, PhoenixScore score, PhoenixPlate plate = PhoenixPlate.FairGame)
        {
            _bests.Add(new RecordedPhoenixScore(chart.Id, score, plate, false, Now));
        }

        public void GivenTop50Pumbility(params int[] scores)
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(scores
                    .Select(s => new RecordedPhoenixScore(Guid.NewGuid(), s, PhoenixPlate.FairGame, false, Now))
                    .ToArray());
        }

        public void GivenTop50Competitive(ChartType type,
            params (Chart Chart, int Score, PhoenixPlate Plate)[] records)
        {
            Mediator.Setup(m => m.Send(It.Is<GetTop50CompetitiveQuery>(q => q.ChartType == type),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(records
                    .Select(r => new RecordedPhoenixScore(r.Chart.Id, r.Score, r.Plate, false, Now))
                    .ToArray());
        }

        public void GivenCohortScores(Chart chart, params int[] ascendingScores)
        {
            var cohortUser = Guid.NewGuid();
            PlayerStats.Setup(p => p.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType?>(),
                    It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { cohortUser });
            Scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.Is<IEnumerable<Guid>>(ids => ids.Contains(chart.Id)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ascendingScores
                    .Select(s => new UserPhoenixScore(cohortUser, chart.Id, Name.From("Peer"), s, null, false))
                    .ToArray());
        }

        public void GivenCompletedTitles(params string[] titles)
        {
            Titles.Setup(t => t.GetCompletedTitles(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(titles
                    .Select(t => new TitleAchievedRecord(UserId, Name.From(t), ParagonLevel.None))
                    .ToArray());
        }

        public void GivenTitleRarity(params (string Title, int Holders)[] aggregations)
        {
            Titles.Setup(t => t.GetTitleAggregations(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(aggregations
                    .Select(a => new TitleAggregationRecord(Name.From(a.Title), a.Holders))
                    .ToArray());
        }

        public void GivenTierList(string name, params (Guid ChartId, TierListCategory Category)[] entries)
        {
            Mediator.Setup(m => m.Send(It.Is<GetTierListQuery>(q => q.TierListName == Name.From(name)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries
                    .Select(e => new SongTierListEntry(Name.From(name), e.ChartId, e.Category, 0))
                    .ToArray());
        }

        public void GivenAllStats(params PlayerStatsRecord[] stats)
        {
            PlayerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(stats);
            var mine = stats.First(s => s.UserId == UserId);
            PlayerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mine);
        }

        public void GivenHistory(params PlayerRatingRecord[] history)
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetPlayerHistoryQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(history);
        }

        public ConsumeContext<CalculateSeasonRecapsCommand> Context(CalculateSeasonRecapsCommand message)
        {
            var ctx = new Mock<ConsumeContext<CalculateSeasonRecapsCommand>>();
            ctx.SetupGet(c => c.Message).Returns(message);
            ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
            return ctx.Object;
        }
    }
}
