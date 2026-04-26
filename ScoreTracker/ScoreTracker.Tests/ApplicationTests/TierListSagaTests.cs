using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TierListSagaTests
{
    [Fact]
    public async Task ChartDifficultyUpdatedSavesNothingWhenNoChartsExist()
    {
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        tierLists.Verify(t => t.SaveEntry(It.IsAny<SongTierListEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChartDifficultyUpdatedSkipsChartsThatHaveNoRating()
    {
        var ratedChart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var unratedChart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single, new[] { ratedChart, unratedChart });
        var ratings = RatingsMockReturning(new[] { Rating(ratedChart.Id, difficulty: 15.5) });
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(charts: charts, chartRatings: ratings, tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        tierLists.Verify(t => t.SaveEntry(
            It.Is<SongTierListEntry>(e => e.ChartId == ratedChart.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        tierLists.Verify(t => t.SaveEntry(
            It.Is<SongTierListEntry>(e => e.ChartId == unratedChart.Id), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // diff = ratingDifficulty - chartLevel - 0.5; the saga's switch cascade puts diff
    // into buckets (-∞,-.75] → Overrated, (-.75,-.375] → VeryEasy, (-.375,-.125] → Easy,
    // (-.125,.125) → Medium, [.125,.375) → Hard, [.375,.75) → VeryHard, [.75,∞) → Underrated.
    // Inputs below land cleanly inside their bucket (chart level fixed at 15).
    [Theory]
    [InlineData(14.0, TierListCategory.Overrated)]   // diff = -1.5
    [InlineData(15.0, TierListCategory.VeryEasy)]    // diff = -0.5
    [InlineData(15.25, TierListCategory.Easy)]       // diff = -0.25
    [InlineData(15.5, TierListCategory.Medium)]      // diff = 0
    [InlineData(15.75, TierListCategory.Hard)]       // diff = 0.25
    [InlineData(16.0, TierListCategory.VeryHard)]    // diff = 0.5
    [InlineData(16.5, TierListCategory.Underrated)]  // diff = 1.0
    public async Task ChartDifficultyUpdatedAssignsCategoryFromDifficultyDelta(
        double ratingDifficulty, TierListCategory expected)
    {
        var chart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single, new[] { chart });
        var ratings = RatingsMockReturning(new[] { Rating(chart.Id, difficulty: ratingDifficulty) });
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(charts: charts, chartRatings: ratings, tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        tierLists.Verify(t => t.SaveEntry(
            It.Is<SongTierListEntry>(e => e.ChartId == chart.Id && e.Category == expected),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChartDifficultyUpdatedAssignsContiguousAscendingOrderStartingAtZero()
    {
        var c1 = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var c2 = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var c3 = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single, new[] { c1, c2, c3 });
        var ratings = RatingsMockReturning(new[]
        {
            Rating(c1.Id, difficulty: 15.5),
            Rating(c2.Id, difficulty: 15.5),
            Rating(c3.Id, difficulty: 15.5)
        });
        var saved = new List<SongTierListEntry>();
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.SaveEntry(It.IsAny<SongTierListEntry>(), It.IsAny<CancellationToken>()))
            .Callback<SongTierListEntry, CancellationToken>((e, _) => saved.Add(e));
        var saga = BuildSaga(charts: charts, chartRatings: ratings, tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        Assert.Equal(new[] { 0, 1, 2 }, saved.Select(e => e.Order).ToArray());
    }

    [Fact]
    public async Task ChartDifficultyUpdatedSavesEntriesUnderDifficultyTierList()
    {
        var chart = new ChartBuilder().WithLevel(15).Build();
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single, new[] { chart });
        var ratings = RatingsMockReturning(new[] { Rating(chart.Id, difficulty: 15.5) });
        var tierLists = new Mock<ITierListRepository>();
        var saga = BuildSaga(charts: charts, chartRatings: ratings, tierLists: tierLists);

        await saga.Consume(BuildContext(new ChartDifficultyUpdatedEvent(ChartType.Single, 15)));

        tierLists.Verify(t => t.SaveEntry(
            It.Is<SongTierListEntry>(e => (string)e.TierListName == "Difficulty"),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RelativeTierListReturnsEmptyWhenUserHasNoMatchingScores()
    {
        var charts = ChartsMockReturning(level: 15, type: ChartType.Single,
            new[] { new ChartBuilder().WithLevel(15).Build() });
        var scores = new Mock<IPhoenixRecordRepository>();
        scores.Setup(s => s.GetRecordedScores(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        var saga = BuildSaga(charts: charts, scores: scores);

        var result = await saga.Handle(
            new GetMyRelativeTierListQuery(ChartType.Single, DifficultyLevel.From(15), Guid.NewGuid()),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(15, "Scores")]
    [InlineData(23, "Scores")]
    [InlineData(24, "Official Scores")]
    [InlineData(28, "Official Scores")]
    public async Task RelativeTierListChoosesTierListNameByLevel(int level, string expectedListName)
    {
        var chart = new ChartBuilder().WithLevel(level).Build();
        var charts = ChartsMockReturning(level: level, type: ChartType.Single, new[] { chart });
        var scores = new Mock<IPhoenixRecordRepository>();
        scores.Setup(s => s.GetRecordedScores(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(chart.Id, 950000, PhoenixPlate.SuperbGame, false,
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
            });
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetAllEntries(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        var saga = BuildSaga(charts: charts, scores: scores, tierLists: tierLists);

        await saga.Handle(
            new GetMyRelativeTierListQuery(ChartType.Single, DifficultyLevel.From(level), Guid.NewGuid()),
            CancellationToken.None);

        tierLists.Verify(t => t.GetAllEntries(
            It.Is<Name>(n => (string)n == expectedListName), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(10, true)]   // included: Range(10, 18) => 10..27
    [InlineData(27, true)]
    [InlineData(9, false)]   // excluded
    [InlineData(28, false)]
    public async Task ProcessPassTierListIteratesLevelsTenThroughTwentySevenInclusive(int level, bool expected)
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        var scores = new Mock<IPhoenixRecordRepository>();
        scores.Setup(s => s.GetPgUsers(It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid UserId, Guid ChartId)>());
        scores.Setup(s => s.GetRecordedScores(It.IsAny<IEnumerable<Guid>>(), It.IsAny<ChartType>(),
                It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetUsersOnLevel(It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(Array.Empty<Guid>());
        var saga = BuildSaga(charts: charts, scores: scores, tierLists: tierLists);

        await saga.Consume(BuildContext(new ProcessPassTierListCommand()));

        var times = expected ? Times.AtLeastOnce() : Times.Never();
        charts.Verify(c => c.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(level), ChartType.Single,
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), times);
    }

    private static TierListSaga BuildSaga(
        Mock<IChartDifficultyRatingRepository>? chartRatings = null,
        Mock<IChartRepository>? charts = null,
        Mock<ITierListRepository>? tierLists = null,
        Mock<IPhoenixRecordRepository>? scores = null,
        Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IPlayerStatsRepository>? playerStats = null)
    {
        chartRatings ??= EmptyRatingsMock();
        charts ??= EmptyChartsMock();
        tierLists ??= new Mock<ITierListRepository>();
        scores ??= new Mock<IPhoenixRecordRepository>();
        currentUser ??= new Mock<ICurrentUserAccessor>();
        playerStats ??= new Mock<IPlayerStatsRepository>();
        return new TierListSaga(chartRatings.Object, charts.Object, tierLists.Object, scores.Object,
            currentUser.Object, playerStats.Object);
    }

    private static Mock<IChartRepository> EmptyChartsMock()
    {
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        return m;
    }

    private static Mock<IChartDifficultyRatingRepository> EmptyRatingsMock()
    {
        var m = new Mock<IChartDifficultyRatingRepository>();
        m.Setup(c => c.GetAllChartRatedDifficulties(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartDifficultyRatingRecord>());
        return m;
    }

    private static Mock<IChartRepository> ChartsMockReturning(int level, ChartType type, IEnumerable<Chart> result)
    {
        var m = EmptyChartsMock();
        m.Setup(c => c.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(level), type,
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return m;
    }

    private static Mock<IChartDifficultyRatingRepository> RatingsMockReturning(
        IEnumerable<ChartDifficultyRatingRecord> ratings)
    {
        var m = new Mock<IChartDifficultyRatingRepository>();
        m.Setup(c => c.GetAllChartRatedDifficulties(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ratings);
        return m;
    }

    private static ChartDifficultyRatingRecord Rating(Guid chartId, double difficulty) =>
        new(chartId, difficulty, RatingCount: 1, StandardDeviation: 0);

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
