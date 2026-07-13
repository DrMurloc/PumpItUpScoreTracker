using ScoreTracker.WeeklyChallenge.Application;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Commands;
using ScoreTracker.WeeklyChallenge.Contracts.Messages;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using ScoreTracker.WeeklyChallenge.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class DailyStepSagaTests
{
    // The Limbo weekday is deterministic per ISO week, so find a concrete normal/limbo day to
    // clock the saga to (each week has exactly one of each within any 14-day span).
    private static readonly DateTimeOffset NormalDay = FindDay(limbo: false);
    private static readonly DateTimeOffset LimboDay = FindDay(limbo: true);

    private static DateTimeOffset FindDay(bool limbo)
    {
        var day = new DateTimeOffset(2026, 6, 1, 5, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 14; i++, day = day.AddDays(1))
            if (DailyStepLimboPolicy.IsLimboDay(day) == limbo)
                return day;
        throw new InvalidOperationException("No matching day within two weeks");
    }

    [Fact]
    public async Task RotationExitsEarlyWhenTodaysBoardIsStillLive()
    {
        var daily = new Mock<IDailyStepRepository>();
        daily.Setup(d => d.GetCurrentChart(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyStepBoard(Guid.NewGuid(), NormalDay, false, NormalDay.AddHours(6)));
        var saga = BuildSaga(daily, now: NormalDay);

        await saga.Consume(Context(new RotateDailyStepCommand()));

        daily.Verify(d => d.ClearBoard(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()), Times.Never);
        daily.Verify(d => d.RegisterDailyChart(It.IsAny<MixEnum>(), It.IsAny<DailyStepBoard>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotationSkipsWhenTheMixHasNoCharts()
    {
        var daily = new Mock<IDailyStepRepository>();
        daily.Setup(d => d.GetCurrentChart(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyStepBoard?)null);
        var charts = ChartsReturning(Array.Empty<Chart>());
        var saga = BuildSaga(daily, charts, now: NormalDay);

        await saga.Consume(Context(new RotateDailyStepCommand(MixEnum.Phoenix2)));

        daily.Verify(d => d.ClearBoard(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()), Times.Never);
        daily.Verify(d => d.RegisterDailyChart(It.IsAny<MixEnum>(), It.IsAny<DailyStepBoard>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotationOnANormalDayDrawsAStandardBandSingleOrDouble()
    {
        var s20 = ChartAt(ChartType.Single, 20);
        var d22 = ChartAt(ChartType.Double, 22);
        var easy = ChartAt(ChartType.Single, 10);   // Limbo band — must not be picked on a normal day
        var coop = ChartAt(ChartType.CoOp, 20);      // co-op excluded
        var daily = FreshBoard();
        var saga = BuildSaga(daily, ChartsReturning(new[] { s20, d22, easy, coop }), now: NormalDay);

        await saga.Consume(Context(new RotateDailyStepCommand()));

        daily.Verify(d => d.RegisterDailyChart(MixEnum.Phoenix,
            It.Is<DailyStepBoard>(b => !b.IsLimbo
                                       && (b.ChartId == s20.Id || b.ChartId == d22.Id)
                                       && b.ExpirationDate == NormalDay.AddDays(1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotationOnALimboDayDrawsAnEasyChartFlaggedLimbo()
    {
        var s20 = ChartAt(ChartType.Single, 20);   // standard band — excluded on Limbo day
        var easy = ChartAt(ChartType.Single, 8);   // 1–15 band, singles allowed
        var daily = FreshBoard();
        var saga = BuildSaga(daily, ChartsReturning(new[] { s20, easy }), now: LimboDay);

        await saga.Consume(Context(new RotateDailyStepCommand()));

        daily.Verify(d => d.RegisterDailyChart(MixEnum.Phoenix,
            It.Is<DailyStepBoard>(b => b.IsLimbo && b.ChartId == easy.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotationSnapshotsTheFinishingBoardThenClears()
    {
        var finishingChart = Guid.NewGuid();
        var entrant = Guid.NewGuid();
        var daily = new Mock<IDailyStepRepository>();
        daily.Setup(d => d.GetCurrentChart(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyStepBoard(finishingChart, NormalDay.AddDays(-1), false, NormalDay.AddHours(-1)));
        daily.Setup(d => d.GetEntries(MixEnum.Phoenix, finishingChart, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Entry(finishingChart, 950_000, entrant) });
        var saga = BuildSaga(daily, ChartsReturning(new[] { ChartAt(ChartType.Single, 20) }), now: NormalDay);

        await saga.Consume(Context(new RotateDailyStepCommand()));

        daily.Verify(d => d.WriteHistories(MixEnum.Phoenix,
            It.Is<IEnumerable<DailyStepPlacing>>(ps => ps.Any(p => p.UserId == entrant && p.Place == 1)),
            It.IsAny<CancellationToken>()), Times.Once);
        daily.Verify(d => d.ClearBoard(MixEnum.Phoenix, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IntakeSavesTheBestScoreForANewEntryOnANormalDay()
    {
        var chart = ChartAt(ChartType.Single, 20);
        var userId = Guid.NewGuid();
        var (daily, saga) = IntakeContext(Board(chart.Id, isLimbo: false), chart, NormalDay);

        await saga.Consume(Context(Observed(userId, chart.Id, best: 950_000, lowestPass: 800_000)));

        daily.Verify(d => d.SaveEntry(MixEnum.Phoenix,
            It.Is<DailyStepEntry>(e => e.UserId == userId && e.Score == (PhoenixScore)950_000
                                       && e.Source == DailyStepSource.Official),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IntakeKeepsAHigherExistingScoreWhenALowerBestIsObservedOnANormalDay()
    {
        var chart = ChartAt(ChartType.Single, 20);
        var userId = Guid.NewGuid();
        var (daily, saga) = IntakeContext(Board(chart.Id, isLimbo: false), chart, NormalDay,
            existing: new[] { Entry(chart.Id, 980_000, userId) });

        await saga.Consume(Context(Observed(userId, chart.Id, best: 900_000, lowestPass: 850_000)));

        daily.Verify(d => d.SaveEntry(It.IsAny<MixEnum>(), It.IsAny<DailyStepEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IntakeSavesTheLowestPassingScoreOnALimboDay()
    {
        var chart = ChartAt(ChartType.Single, 10);
        var userId = Guid.NewGuid();
        // Existing high score should be REPLACED by the lower passing run on Limbo day.
        var (daily, saga) = IntakeContext(Board(chart.Id, isLimbo: true), chart, LimboDay,
            existing: new[] { Entry(chart.Id, 990_000, userId) });

        await saga.Consume(Context(Observed(userId, chart.Id, best: 990_000, lowestPass: 720_000)));

        daily.Verify(d => d.SaveEntry(MixEnum.Phoenix,
            It.Is<DailyStepEntry>(e => e.UserId == userId && e.Score == (PhoenixScore)720_000
                                       && !e.IsBroken && e.Source == DailyStepSource.Official),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IntakeSkipsOnALimboDayWhenNoRunPassed()
    {
        var chart = ChartAt(ChartType.Single, 10);
        var userId = Guid.NewGuid();
        var (daily, saga) = IntakeContext(Board(chart.Id, isLimbo: true), chart, LimboDay);

        await saga.Consume(Context(Observed(userId, chart.Id, best: 700_000, lowestPass: null,
            bestIsBroken: true)));

        daily.Verify(d => d.SaveEntry(It.IsAny<MixEnum>(), It.IsAny<DailyStepEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IntakeSkipsWhenTheObservedChartIsNotTodaysBoard()
    {
        var boardChart = ChartAt(ChartType.Single, 20);
        var (daily, saga) = IntakeContext(Board(boardChart.Id, isLimbo: false), boardChart, NormalDay);

        await saga.Consume(Context(Observed(Guid.NewGuid(), Guid.NewGuid(), best: 950_000, lowestPass: 900_000)));

        daily.Verify(d => d.SaveEntry(It.IsAny<MixEnum>(), It.IsAny<DailyStepEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PlacementQueryRanksTheCallerAndCountsTheBoard()
    {
        var chart = ChartAt(ChartType.Single, 20);
        var me = Guid.NewGuid();
        var daily = new Mock<IDailyStepRepository>();
        daily.Setup(d => d.GetCurrentChart(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Board(chart.Id, isLimbo: false));
        daily.Setup(d => d.GetEntries(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Entry(chart.Id, 990_000, Guid.NewGuid()),
                Entry(chart.Id, 950_000, me),
                Entry(chart.Id, 900_000, Guid.NewGuid())
            });
        var saga = BuildSaga(daily, now: NormalDay);

        var placement = await saga.Handle(new GetDailyStepPlacementQuery(me), CancellationToken.None);

        Assert.NotNull(placement);
        Assert.Equal(2, placement!.Place);
        Assert.Equal(3, placement.Total);
        Assert.False(placement.IsLimbo);
    }

    [Fact]
    public async Task ManualRecordSavesTheSubmittedScoreStampedManual()
    {
        var chart = ChartAt(ChartType.Single, 20);
        var me = new UserBuilder().Build();
        var meId = me.Id;
        var (daily, saga) = IntakeContext(Board(chart.Id, isLimbo: false), chart, NormalDay,
            currentUser: CurrentUserReturning(me));

        await saga.Handle(new RecordDailyStepScoreCommand(875_000, PhoenixPlate.MarvelousGame),
            CancellationToken.None);

        daily.Verify(d => d.SaveEntry(MixEnum.Phoenix,
            It.Is<DailyStepEntry>(e => e.UserId == meId && e.Score == (PhoenixScore)875_000
                                       && e.Plate == PhoenixPlate.MarvelousGame && !e.IsBroken
                                       && e.Source == DailyStepSource.Manual),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ManualLimboLowReplacesAHigherOfficialEntry()
    {
        var chart = ChartAt(ChartType.Single, 10);
        var me = new UserBuilder().Build();
        var meId = me.Id;
        // A deliberate low pass typed into the widget beats the player's official best on Limbo day.
        var (daily, saga) = IntakeContext(Board(chart.Id, isLimbo: true), chart, LimboDay,
            existing: new[] { Entry(chart.Id, 970_000, meId) },
            currentUser: CurrentUserReturning(me));

        await saga.Handle(new RecordDailyStepScoreCommand(680_000, PhoenixPlate.FairGame),
            CancellationToken.None);

        daily.Verify(d => d.SaveEntry(MixEnum.Phoenix,
            It.Is<DailyStepEntry>(e => e.UserId == meId && e.Score == (PhoenixScore)680_000
                                       && e.Source == DailyStepSource.Manual),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- fixtures ---------------------------------------------------------------------------

    private static Mock<IDailyStepRepository> FreshBoard()
    {
        var daily = new Mock<IDailyStepRepository>();
        daily.Setup(d => d.GetCurrentChart(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyStepBoard?)null);
        return daily;
    }

    private static (Mock<IDailyStepRepository> Daily, DailyStepSaga Saga) IntakeContext(
        DailyStepBoard board, Chart chart, DateTimeOffset now,
        IEnumerable<DailyStepEntry>? existing = null, Mock<ICurrentUserAccessor>? currentUser = null)
    {
        var daily = new Mock<IDailyStepRepository>();
        daily.Setup(d => d.GetCurrentChart(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(board);
        daily.Setup(d => d.GetEntries(It.IsAny<MixEnum>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing ?? Array.Empty<DailyStepEntry>());
        var stats = new Mock<IPlayerStatsReader>();
        stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(singles: 18.5, doubles: 12.0));
        return (daily, BuildSaga(daily, ChartsReturning(new[] { chart }), stats, now, currentUser));
    }

    private static DailyStepBoard Board(Guid chartId, bool isLimbo) =>
        new(chartId, NormalDay, isLimbo, NormalDay.AddDays(1));

    private static DailyStepScoreObservedEvent Observed(Guid userId, Guid chartId, int best, int? lowestPass,
        bool bestIsBroken = false) =>
        new(userId, MixEnum.Phoenix, chartId, best, PhoenixPlate.SuperbGame.ToString(), bestIsBroken,
            lowestPass, lowestPass == null ? null : PhoenixPlate.SuperbGame.ToString());

    private static Chart ChartAt(ChartType type, int level) =>
        new ChartBuilder().WithType(type).WithLevel(level).WithSongName($"{type}-{level}").Build();

    private static DailyStepEntry Entry(Guid chartId, int score, Guid userId, bool isBroken = false,
        DailyStepSource source = DailyStepSource.Official) =>
        new(userId, chartId, score, PhoenixPlate.SuperbGame, isBroken, 20.0, source);

    private static Mock<IChartRepository> ChartsReturning(IEnumerable<Chart> charts)
    {
        var mock = new Mock<IChartRepository>();
        mock.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        return mock;
    }

    private static PlayerStatsRecord Stats(double singles, double doubles) =>
        new(Guid.NewGuid(), TotalRating: 0, HighestLevel: 1, ClearCount: 0, CoOpRating: 0, CoOpScore: 0,
            SkillRating: 0, SkillScore: 0, SkillLevel: 0, SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0,
            DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0, CompetitiveLevel: (singles + doubles) / 2,
            SinglesCompetitiveLevel: singles, DoublesCompetitiveLevel: doubles);

    private static DailyStepSaga BuildSaga(
        Mock<IDailyStepRepository> daily,
        Mock<IChartRepository>? charts = null,
        Mock<IPlayerStatsReader>? playerStats = null,
        DateTimeOffset? now = null,
        Mock<ICurrentUserAccessor>? currentUser = null)
    {
        charts ??= new Mock<IChartRepository>();
        playerStats ??= new Mock<IPlayerStatsReader>();
        currentUser ??= CurrentUserReturning(new UserBuilder().Build());
        var random = new Mock<IRandomNumberGenerator>();
        random.Setup(r => r.Next(It.IsAny<int>())).Returns(0);
        return new DailyStepSaga(daily.Object, charts.Object, playerStats.Object, currentUser.Object,
            FakeDateTime.At(now ?? NormalDay).Object, random.Object, NullLogger<DailyStepSaga>.Instance);
    }

    private static Mock<ICurrentUserAccessor> CurrentUserReturning(User user)
    {
        var mock = new Mock<ICurrentUserAccessor>();
        mock.Setup(c => c.User).Returns(user);
        mock.Setup(c => c.IsLoggedIn).Returns(true);
        return mock;
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
