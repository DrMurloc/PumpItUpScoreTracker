using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class WeeklyTournamentSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConsumeUpdateWeeklyChartsExitsEarlyWhenAnyCurrentWeekIsStillActive()
    {
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                // Expiration date in the future relative to `Now` → week still active.
                new WeeklyTournamentChart(Guid.NewGuid(), Now.AddDays(2))
            });
        var saga = BuildSaga(weeklyTournies: weeklyTournies);

        await saga.Consume(BuildContext(new UpdateWeeklyChartsEvent()));

        weeklyTournies.Verify(w => w.ClearTheBoard(It.IsAny<CancellationToken>()), Times.Never);
        weeklyTournies.Verify(w => w.RegisterWeeklyChart(It.IsAny<WeeklyTournamentChart>(),
            It.IsAny<CancellationToken>()), Times.Never);
        weeklyTournies.Verify(w => w.WriteHistories(It.IsAny<IEnumerable<UserTourneyHistory>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeUpdateWeeklyChartsWritesHistoriesAndClearsBoardWhenWeekExpired()
    {
        // Now is 2026-05-01 12:00 UTC; an expired chart (ExpirationDate < Now) lets the
        // chart-picker run end-to-end with a complete required-bucket fixture.
        var ctx = ExpiredWeekContext();
        var entryUser = Guid.NewGuid();
        var entryChart = Guid.NewGuid();
        ctx.WeeklyTournies.Setup(w => w.GetEntries(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentEntry(entryUser, entryChart, 950000, PhoenixPlate.SuperbGame,
                    IsBroken: false, PhotoUrl: null, CompetitiveLevel: 20)
            });

        await ctx.Saga.Consume(BuildContext(new UpdateWeeklyChartsEvent()));

        ctx.WeeklyTournies.Verify(w => w.WriteHistories(
            It.Is<IEnumerable<UserTourneyHistory>>(hs => hs.Any(h => h.UserId == entryUser
                                                                    && h.ChartId == entryChart)),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.WeeklyTournies.Verify(w => w.ClearTheBoard(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeUpdateWeeklyChartsRegistersExactlyOneChartPerMergedBucket()
    {
        // Required-bucket fixture has 8 charts; after merging CoOp 4-5 → 3,
        // S26 → S25, and D28+D29 → D27, only 3 buckets remain. Each gets one chart.
        var ctx = ExpiredWeekContext();
        var nextMonday3am = new DateTimeOffset(2026, 5, 4, 3, 0, 0, TimeSpan.Zero);

        await ctx.Saga.Consume(BuildContext(new UpdateWeeklyChartsEvent()));

        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(
            It.Is<WeeklyTournamentChart>(c => c.ExpirationDate == nextMonday3am),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ConsumeUpdateWeeklyChartsExcludesAlreadyPlayedChartsWhenPicking()
    {
        // Two CoOp 3 charts; one is in alreadyPlayed, so only the unplayed one can be picked.
        var ctx = ExpiredWeekContext();
        var unplayed = ctx.Charts["coop3"];
        var played = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).WithSongName("played").Build();
        ctx.GivenChartList(ReplaceCoOp3WithBoth(ctx.Charts, played));
        ctx.WeeklyTournies.Setup(w => w.GetAlreadyPlayedCharts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { played.Id });

        await ctx.Saga.Consume(BuildContext(new UpdateWeeklyChartsEvent()));

        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(
            It.Is<WeeklyTournamentChart>(c => c.ChartId == unplayed.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(
            It.Is<WeeklyTournamentChart>(c => c.ChartId == played.Id),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeUpdateWeeklyChartsResetsAlreadyPlayedListWhenAllChartsInBucketAreExhausted()
    {
        // All 3 charts in the merged CoOp 3 bucket are in alreadyPlayed → no valid
        // candidates remain after filtering, so the algorithm clears the already-played
        // list for that bucket and falls back to picking from the full set.
        var ctx = ExpiredWeekContext();
        var coop3 = ctx.Charts["coop3"];
        var coop4 = ctx.Charts["coop4"];
        var coop5 = ctx.Charts["coop5"];
        ctx.WeeklyTournies.Setup(w => w.GetAlreadyPlayedCharts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { coop3.Id, coop4.Id, coop5.Id });

        await ctx.Saga.Consume(BuildContext(new UpdateWeeklyChartsEvent()));

        ctx.WeeklyTournies.Verify(w => w.ClearAlreadyPlayedCharts(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(coop3.Id) && ids.Contains(coop4.Id)
                                             && ids.Contains(coop5.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(
            It.Is<WeeklyTournamentChart>(c => c.ChartId == coop3.Id || c.ChartId == coop4.Id
                                              || c.ChartId == coop5.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeUpdateWeeklyChartsMergesCoOpFourAndFiveIntoCoOpThreeBucket()
    {
        // Mark the CoOp 3 and CoOp 5 charts as already played. After the merge moves
        // levels 4 and 5 into the level-3 bucket, only the CoOp-4 chart is unplayed
        // — and the algorithm only iterates the merged (3, CoOp) bucket, so this
        // chart can ONLY be picked if the merge actually happened.
        var ctx = ExpiredWeekContext();
        var coop4 = ctx.Charts["coop4"];
        ctx.WeeklyTournies.Setup(w => w.GetAlreadyPlayedCharts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ctx.Charts["coop3"].Id, ctx.Charts["coop5"].Id });

        await ctx.Saga.Consume(BuildContext(new UpdateWeeklyChartsEvent()));

        ctx.WeeklyTournies.Verify(w => w.RegisterWeeklyChart(
            It.Is<WeeklyTournamentChart>(c => c.ChartId == coop4.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRegisterWeeklyChartScoreSkipsWhenChartNotInCurrentWeek()
    {
        var someOtherChartId = Guid.NewGuid();
        var weeklyTournies = new Mock<IWeeklyTournamentRepository>();
        weeklyTournies.Setup(w => w.GetWeeklyCharts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyTournamentChart(someOtherChartId, Now.AddDays(2)) });
        var saga = BuildSaga(weeklyTournies: weeklyTournies);

        var requestedChartId = Guid.NewGuid();
        await saga.Handle(
            new RegisterWeeklyChartScore(Entry(requestedChartId, score: 950000)),
            CancellationToken.None);

        weeklyTournies.Verify(w => w.SaveEntry(It.IsAny<WeeklyTournamentEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleRegisterWeeklyChartScoreSavesEntryWithComputedCompetitiveLevelWhenNew()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 18.5, doublesCompetitive: 12.0);
        ctx.GivenNoExistingEntries(chart.Id);

        var userId = Guid.NewGuid();
        await ctx.Saga.Handle(
            new RegisterWeeklyChartScore(Entry(chart.Id, score: 950000, userId: userId)),
            CancellationToken.None);

        // Single chart → uses SinglesCompetitiveLevel.
        ctx.WeeklyTournies.Verify(w => w.SaveEntry(
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && e.Score == (PhoenixScore)950000
                                              && e.CompetitiveLevel == 18.5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRegisterWeeklyChartScoreKeepsHigherExistingScoreWhenLowerSubmitted()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id, new[]
        {
            Entry(chart.Id, score: 950000, userId: userId)
        });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScore(Entry(chart.Id, score: 800000, userId: userId)),
            CancellationToken.None);

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && e.Score == (PhoenixScore)950000),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRegisterWeeklyChartScoreReplacesScoreWhenHigherSubmitted()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id, new[]
        {
            Entry(chart.Id, score: 800000, userId: userId)
        });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScore(Entry(chart.Id, score: 990000, userId: userId)),
            CancellationToken.None);

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && e.Score == (PhoenixScore)990000),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRegisterWeeklyChartScoreClearsBrokenWhenSubmissionIsUnbroken()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id, new[]
        {
            Entry(chart.Id, score: 950000, userId: userId, isBroken: true)
        });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScore(Entry(chart.Id, score: 950000, userId: userId, isBroken: false)),
            CancellationToken.None);

        ctx.WeeklyTournies.Verify(w => w.SaveEntry(
            It.Is<WeeklyTournamentEntry>(e => e.UserId == userId && !e.IsBroken),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRegisterWeeklyChartScorePublishesEventWhenPlaceChanges()
    {
        // No existing entry → existingPlace == null → place change → publish.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenNoExistingEntries(chart.Id);
        ctx.Users.Setup(u => u.GetUser(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(userId).Build());

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScore(Entry(chart.Id, score: 950000, userId: userId)),
            CancellationToken.None);

        ctx.Bus.Verify(b => b.Publish(
            It.Is<UserWeeklyChartsProgressedEvent>(e => e.UserId == userId && e.ChartId == chart.Id
                                                       && e.Score == 950000 && e.Place == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRegisterWeeklyChartScoreDoesNotPublishWhenPlaceUnchanged()
    {
        // Solo player improving their own (already 1st) score keeps place = 1 → no publish.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(chart);
        ctx.GivenStats(singlesCompetitive: 20, doublesCompetitive: 20);
        ctx.GivenExistingEntries(chart.Id, new[]
        {
            Entry(chart.Id, score: 900000, userId: userId)
        });

        await ctx.Saga.Handle(
            new RegisterWeeklyChartScore(Entry(chart.Id, score: 950000, userId: userId)),
            CancellationToken.None);

        ctx.Bus.Verify(b => b.Publish(It.IsAny<UserWeeklyChartsProgressedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class HandlerContext
    {
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IWeeklyTournamentRepository> WeeklyTournies { get; } = new();
        public Mock<IPlayerStatsRepository> PlayerStats { get; } = new();
        public Mock<IBotClient> Bot { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IBus> Bus { get; } = new();
        public WeeklyTournamentSaga Saga { get; }

        public HandlerContext(Chart chart)
        {
            WeeklyTournies.Setup(w => w.GetWeeklyCharts(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new WeeklyTournamentChart(chart.Id, Now.AddDays(2)) });
            Charts.Setup(c => c.GetCharts(MixEnum.Phoenix, It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(),
                    It.Is<IEnumerable<Guid>>(ids => ids != null),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { chart });

            Saga = BuildSaga(charts: Charts, weeklyTournies: WeeklyTournies, playerStats: PlayerStats,
                bot: Bot, users: Users, bus: Bus);
        }

        public void GivenStats(double singlesCompetitive, double doublesCompetitive)
        {
            PlayerStats.Setup(s => s.GetStats(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlayerStatsRecord(Guid.NewGuid(), TotalRating: 0, HighestLevel: 1,
                    ClearCount: 0, CoOpRating: 0, CoOpScore: 0, SkillRating: 0, SkillScore: 0,
                    SkillLevel: 0, SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0,
                    DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
                    CompetitiveLevel: (singlesCompetitive + doublesCompetitive) / 2,
                    SinglesCompetitiveLevel: singlesCompetitive,
                    DoublesCompetitiveLevel: doublesCompetitive));
        }

        public void GivenNoExistingEntries(Guid chartId)
        {
            WeeklyTournies.Setup(w => w.GetEntries(chartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<WeeklyTournamentEntry>());
        }

        public void GivenExistingEntries(Guid chartId, IEnumerable<WeeklyTournamentEntry> entries)
        {
            WeeklyTournies.Setup(w => w.GetEntries(chartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries);
        }
    }

    private static ChartPickerContext ExpiredWeekContext()
    {
        var ctx = new ChartPickerContext();
        // GetWeeklyCharts is checked twice (early-return guard, then reused). Return
        // an expired chart so the chart-picker proceeds.
        ctx.WeeklyTournies.Setup(w => w.GetWeeklyCharts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentChart(Guid.NewGuid(), Now.AddDays(-1))
            });
        ctx.WeeklyTournies.Setup(w => w.GetEntries(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<WeeklyTournamentEntry>());
        ctx.WeeklyTournies.Setup(w => w.GetAlreadyPlayedCharts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        ctx.GivenChartList(ctx.Charts.Values);
        return ctx;
    }

    private static IEnumerable<Chart> ReplaceCoOp3WithBoth(IDictionary<string, Chart> charts, Chart additional)
    {
        return charts.Values.Append(additional);
    }

    private sealed class ChartPickerContext
    {
        public Mock<IChartRepository> Charts_ { get; } = new();
        public Mock<IWeeklyTournamentRepository> WeeklyTournies { get; } = new();
        public Mock<IPlayerStatsRepository> PlayerStats { get; } = new();
        public Mock<IBotClient> Bot { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IBus> Bus { get; } = new();
        public Mock<IRandomNumberGenerator> Random { get; } = new();
        public IDictionary<string, Chart> Charts { get; }
        public WeeklyTournamentSaga Saga { get; private set; }

        public ChartPickerContext()
        {
            Charts = BuildRequiredChartFixture();
            Random.Setup(r => r.Next(It.IsAny<int>())).Returns(0);
            Saga = BuildSaga(charts: Charts_, weeklyTournies: WeeklyTournies, playerStats: PlayerStats,
                bot: Bot, users: Users, bus: Bus, random: Random);
        }

        public void GivenChartList(IEnumerable<Chart> charts)
        {
            Charts_.Setup(c => c.GetCharts(MixEnum.Phoenix, It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(),
                    It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
        }

        // The chart-picker hard-codes lookups for these (level, type) buckets and crashes
        // if any are missing — a known fragility of the algorithm.
        private static IDictionary<string, Chart> BuildRequiredChartFixture() =>
            new Dictionary<string, Chart>
            {
                ["coop3"] = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).WithSongName("c3").Build(),
                ["coop4"] = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(4).WithSongName("c4").Build(),
                ["coop5"] = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(5).WithSongName("c5").Build(),
                ["single25"] = new ChartBuilder().WithType(ChartType.Single).WithLevel(25).WithSongName("s25").Build(),
                ["single26"] = new ChartBuilder().WithType(ChartType.Single).WithLevel(26).WithSongName("s26").Build(),
                ["double27"] = new ChartBuilder().WithType(ChartType.Double).WithLevel(27).WithSongName("d27").Build(),
                ["double28"] = new ChartBuilder().WithType(ChartType.Double).WithLevel(28).WithSongName("d28").Build(),
                ["double29"] = new ChartBuilder().WithType(ChartType.Double).WithLevel(29).WithSongName("d29").Build()
            };
    }

    private static WeeklyTournamentSaga BuildSaga(
        Mock<IChartRepository>? charts = null,
        Mock<IWeeklyTournamentRepository>? weeklyTournies = null,
        Mock<IPlayerStatsRepository>? playerStats = null,
        Mock<IBotClient>? bot = null,
        Mock<IUserRepository>? users = null,
        Mock<IBus>? bus = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null,
        Mock<IRandomNumberGenerator>? random = null)
    {
        charts ??= new Mock<IChartRepository>();
        weeklyTournies ??= new Mock<IWeeklyTournamentRepository>();
        playerStats ??= new Mock<IPlayerStatsRepository>();
        bot ??= new Mock<IBotClient>();
        users ??= new Mock<IUserRepository>();
        bus ??= new Mock<IBus>();
        dateTime ??= FakeDateTime.At(Now);
        random ??= new Mock<IRandomNumberGenerator>();
        return new WeeklyTournamentSaga(charts.Object, weeklyTournies.Object, playerStats.Object,
            bot.Object, NullLogger<WeeklyTournamentSaga>.Instance, users.Object, bus.Object,
            dateTime.Object, random.Object);
    }

    private static WeeklyTournamentEntry Entry(Guid chartId, int score, Guid? userId = null,
        bool isBroken = false) =>
        new(UserId: userId ?? Guid.NewGuid(), ChartId: chartId, Score: score,
            Plate: PhoenixPlate.SuperbGame, IsBroken: isBroken, PhotoUrl: null,
            CompetitiveLevel: 20);

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
