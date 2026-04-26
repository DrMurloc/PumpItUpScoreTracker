using System;
using System.Collections.Generic;
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

    private static WeeklyTournamentSaga BuildSaga(
        Mock<IChartRepository>? charts = null,
        Mock<IWeeklyTournamentRepository>? weeklyTournies = null,
        Mock<IPlayerStatsRepository>? playerStats = null,
        Mock<IBotClient>? bot = null,
        Mock<IUserRepository>? users = null,
        Mock<IBus>? bus = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null)
    {
        charts ??= new Mock<IChartRepository>();
        weeklyTournies ??= new Mock<IWeeklyTournamentRepository>();
        playerStats ??= new Mock<IPlayerStatsRepository>();
        bot ??= new Mock<IBotClient>();
        users ??= new Mock<IUserRepository>();
        bus ??= new Mock<IBus>();
        dateTime ??= FakeDateTime.At(Now);
        return new WeeklyTournamentSaga(charts.Object, weeklyTournies.Object, playerStats.Object,
            bot.Object, NullLogger<WeeklyTournamentSaga>.Instance, users.Object, bus.Object,
            dateTime.Object);
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
