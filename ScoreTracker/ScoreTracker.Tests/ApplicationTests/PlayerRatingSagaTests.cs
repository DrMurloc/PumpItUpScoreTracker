using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.PersonalProgress;
using ScoreTracker.PersonalProgress.Queries;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerRatingSagaTests
{
    [Fact]
    public async Task UserCreatedSavesZeroedStatsRecord()
    {
        var stats = new Mock<IPlayerStatsRepository>();
        var saga = BuildSaga(stats: stats);
        var userId = Guid.NewGuid();

        await saga.Consume(BuildContext(new UserCreatedEvent(userId)));

        stats.Verify(s => s.SaveStats(userId,
            It.Is<PlayerStatsRecord>(p => p.UserId == userId && p.TotalRating == 0
                                          && p.ClearCount == 0 && p.HighestLevel == DifficultyLevel.From(1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTop50ForPlayerExcludesCoOpCharts()
    {
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var coOp = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var charts = ChartsMockReturning(new[] { single, coOp });
        var scores = ScoresMockReturning(Guid.NewGuid(), new[]
        {
            Score(single.Id, 950000),
            Score(coOp.Id, 990000)
        });
        var saga = BuildSaga(charts: charts, scores: scores);

        var result = (await saga.Handle(
            new GetTop50ForPlayerQuery(Guid.NewGuid(), ChartType: null),
            CancellationToken.None)).ToArray();

        Assert.Single(result);
        Assert.Equal(single.Id, result[0].ChartId);
    }

    [Fact]
    public async Task GetTop50ForPlayerFiltersByChartTypeWhenSpecified()
    {
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var dbl = new ChartBuilder().WithType(ChartType.Double).WithLevel(17).Build();
        var charts = ChartsMockReturning(new[] { single, dbl });
        var scores = ScoresMockReturning(Guid.NewGuid(), new[]
        {
            Score(single.Id, 950000),
            Score(dbl.Id, 950000)
        });
        var saga = BuildSaga(charts: charts, scores: scores);

        var result = (await saga.Handle(
            new GetTop50ForPlayerQuery(Guid.NewGuid(), ChartType.Double),
            CancellationToken.None)).ToArray();

        Assert.Single(result);
        Assert.Equal(dbl.Id, result[0].ChartId);
    }

    [Fact]
    public async Task GetTop50ForPlayerRespectsCountLimitAndOrdersByRatingDescending()
    {
        var c1 = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var c2 = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var c3 = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var charts = ChartsMockReturning(new[] { c1, c2, c3 });
        // Higher score = higher Pumbility rating for same chart type/level.
        var scores = ScoresMockReturning(Guid.NewGuid(), new[]
        {
            Score(c1.Id, 800000),
            Score(c2.Id, 990000),
            Score(c3.Id, 900000)
        });
        var saga = BuildSaga(charts: charts, scores: scores);

        var result = (await saga.Handle(
            new GetTop50ForPlayerQuery(Guid.NewGuid(), ChartType: null, Count: 2),
            CancellationToken.None)).ToArray();

        Assert.Equal(2, result.Length);
        Assert.Equal(c2.Id, result[0].ChartId); // 990000 first
        Assert.Equal(c3.Id, result[1].ChartId); // 900000 second
    }

    [Theory]
    [InlineData(null, 100)]
    [InlineData(ChartType.Single, 50)]
    [InlineData(ChartType.Double, 50)]
    public async Task GetTop50CompetitiveTakesOneHundredForAllAndFiftyForFilteredType(
        ChartType? requestType, int expectedTakeCount)
    {
        // Build 120 charts so the take limit is observable for both 100 (no filter) and 50 (filtered).
        var charts = Enumerable.Range(0, 120)
            .Select(i => new ChartBuilder().WithType(i % 2 == 0 ? ChartType.Single : ChartType.Double)
                .WithLevel(15 + (i % 5)).Build())
            .ToArray();
        var chartsMock = ChartsMockReturning(charts);
        var scores = ScoresMockReturning(Guid.NewGuid(),
            charts.Select((c, i) => Score(c.Id, 800000 + i * 100)).ToArray());
        var saga = BuildSaga(charts: chartsMock, scores: scores);

        var result = (await saga.Handle(
            new GetTop50CompetitiveQuery(Guid.NewGuid(), requestType),
            CancellationToken.None)).ToArray();

        var matching = requestType == null
            ? charts.Length
            : charts.Count(c => c.Type == requestType);
        Assert.Equal(Math.Min(expectedTakeCount, matching), result.Length);
    }

    [Fact]
    public async Task RecalculateStatsSavesNewStatsAndAlwaysPublishesStatsUpdatedEvent()
    {
        var userId = Guid.NewGuid();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(userId, It.IsAny<CancellationToken>())).ReturnsAsync(ZeroStats(userId));
        var bus = new Mock<IBus>();
        var mediator = new Mock<IMediator>();
        var saga = BuildSaga(charts: ChartsMockReturning(Array.Empty<Chart>()),
            scores: ScoresMockReturning(userId, Array.Empty<RecordedPhoenixScore>()),
            stats: stats, bus: bus, mediator: mediator);

        await saga.Handle(new PlayerRatingSaga.RecalculateStats(userId), CancellationToken.None);

        stats.Verify(s => s.SaveStats(userId, It.IsAny<PlayerStatsRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(b => b.Publish(It.Is<PlayerStatsUpdatedEvent>(e => e.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Publish(It.Is<PlayerStatsUpdatedEvent>(e => e.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecalculateStatsPublishesRatingsImprovedWhenSkillRatingIncreases()
    {
        var userId = Guid.NewGuid();
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(userId, It.IsAny<CancellationToken>())).ReturnsAsync(ZeroStats(userId));
        var bus = new Mock<IBus>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(new[] { single }),
            scores: ScoresMockReturning(userId, new[] { Score(single.Id, 950000) }),
            stats: stats, bus: bus);

        await saga.Handle(new PlayerRatingSaga.RecalculateStats(userId), CancellationToken.None);

        bus.Verify(b => b.Publish(It.Is<PlayerRatingsImprovedEvent>(e => e.UserId == userId
                                                                         && e.NewTop50 > e.OldTop50),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecalculateStatsDoesNotPublishRatingsImprovedWhenNothingImproves()
    {
        var userId = Guid.NewGuid();
        var stats = new Mock<IPlayerStatsRepository>();
        // Old stats already at high values — with no new scores, new stats are all 0,
        // so nothing exceeds old stats and no PlayerRatingsImprovedEvent should fire.
        stats.Setup(s => s.GetStats(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(userId, TotalRating: 999999, HighestLevel: 25,
                ClearCount: 1000, CoOpRating: 999999, CoOpScore: 1000000, SkillRating: 999999, SkillScore: 1000000,
                SkillLevel: 25, SinglesRating: 999999, SinglesScore: 1000000, SinglesLevel: 25,
                DoublesRating: 999999, DoublesScore: 1000000, DoublesLevel: 25, CompetitiveLevel: 25,
                SinglesCompetitiveLevel: 25, DoublesCompetitiveLevel: 25));
        var bus = new Mock<IBus>();
        var saga = BuildSaga(
            charts: ChartsMockReturning(Array.Empty<Chart>()),
            scores: ScoresMockReturning(userId, Array.Empty<RecordedPhoenixScore>()),
            stats: stats, bus: bus);

        await saga.Handle(new PlayerRatingSaga.RecalculateStats(userId), CancellationToken.None);

        bus.Verify(b => b.Publish(It.IsAny<PlayerRatingsImprovedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecalculatePumbilityUpdatesScoreStatsForGivenCharts()
    {
        var userId = Guid.NewGuid();
        var c1 = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var c2 = new ChartBuilder().WithType(ChartType.Single).WithLevel(17).Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix, It.IsAny<DifficultyLevel?>(),
                It.IsAny<ChartType?>(),
                It.Is<IEnumerable<Guid>>(ids => ids != null && ids.Contains(c1.Id) && ids.Contains(c2.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { c1, c2 });
        var scores = new Mock<IPhoenixRecordRepository>();
        scores.Setup(s => s.GetPlayerScores(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(userId)),
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(c1.Id) && ids.Contains(c2.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserPhoenixScore(userId, c1.Id, Name.From("alice"), 950000, PhoenixPlate.SuperbGame, false),
                new UserPhoenixScore(userId, c2.Id, Name.From("alice"), 900000, PhoenixPlate.MarvelousGame, false)
            });
        var saga = BuildSaga(charts: charts, scores: scores);

        await saga.Handle(
            new PlayerRatingSaga.RecalculatePumbility(userId, new[] { c1.Id, c2.Id }),
            CancellationToken.None);

        scores.Verify(s => s.UpdateScoreStats(userId,
            It.Is<IEnumerable<PhoenixRecordStats>>(stats =>
                stats.Count() == 2 && stats.Any(p => p.ChartId == c1.Id) && stats.Any(p => p.ChartId == c2.Id)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlayerScoreUpdatedRecalculatesStatsAndPumbility()
    {
        var userId = Guid.NewGuid();
        var chartId = Guid.NewGuid();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(userId, It.IsAny<CancellationToken>())).ReturnsAsync(ZeroStats(userId));
        var scores = new Mock<IPhoenixRecordRepository>();
        scores.Setup(s => s.GetRecordedScores(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        scores.Setup(s => s.GetPlayerScores(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPhoenixScore>());
        var saga = BuildSaga(charts: ChartsMockReturning(Array.Empty<Chart>()),
            scores: scores, stats: stats);

        await saga.Consume(BuildContext(new PlayerScoreUpdatedEvent(userId,
            NewChartIds: new[] { chartId },
            UpscoredChartIds: new Dictionary<Guid, int>())));

        // RecalculateStats path → SaveStats called; RecalculatePumbility path → UpdateScoreStats called.
        stats.Verify(s => s.SaveStats(userId, It.IsAny<PlayerStatsRecord>(),
            It.IsAny<CancellationToken>()), Times.Once);
        scores.Verify(s => s.UpdateScoreStats(userId, It.IsAny<IEnumerable<PhoenixRecordStats>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PlayerRatingSaga BuildSaga(
        Mock<IPhoenixRecordRepository>? scores = null,
        Mock<IChartRepository>? charts = null,
        Mock<IPlayerStatsRepository>? stats = null,
        Mock<IBus>? bus = null,
        Mock<IMediator>? mediator = null)
    {
        scores ??= new Mock<IPhoenixRecordRepository>();
        charts ??= new Mock<IChartRepository>();
        stats ??= new Mock<IPlayerStatsRepository>();
        bus ??= new Mock<IBus>();
        mediator ??= new Mock<IMediator>();
        return new PlayerRatingSaga(scores.Object, charts.Object, stats.Object, bus.Object, mediator.Object);
    }

    private static Mock<IChartRepository> ChartsMockReturning(IEnumerable<Chart> result)
    {
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetCharts(MixEnum.Phoenix, It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return m;
    }

    private static Mock<IPhoenixRecordRepository> ScoresMockReturning(Guid userId,
        IEnumerable<RecordedPhoenixScore> result)
    {
        var m = new Mock<IPhoenixRecordRepository>();
        m.Setup(s => s.GetRecordedScores(userId, It.IsAny<CancellationToken>())).ReturnsAsync(result);
        m.Setup(s => s.GetRecordedScores(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return m;
    }

    private static RecordedPhoenixScore Score(Guid chartId, int score, bool isBroken = false) =>
        new(chartId, score, PhoenixPlate.SuperbGame, isBroken,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static PlayerStatsRecord ZeroStats(Guid userId) =>
        new(userId, TotalRating: 0, HighestLevel: 1, ClearCount: 0, CoOpRating: 0, CoOpScore: 0,
            SkillRating: 0, SkillScore: 0, SkillLevel: 0, SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0,
            DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0, CompetitiveLevel: 0,
            SinglesCompetitiveLevel: 0, DoublesCompetitiveLevel: 0);

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
