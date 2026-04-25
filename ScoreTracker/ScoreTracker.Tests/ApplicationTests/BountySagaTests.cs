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
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class BountySagaTests
{
    [Fact]
    public async Task ConsumeUpdateBountiesClearsMonthlyBoardOnFirstOfMonth()
    {
        var bounties = new Mock<IChartBountyRepository>();
        var saga = BuildSaga(bounties: bounties, dateTime: FakeDateTime.At(2026, 5, 1));

        await saga.Consume(BuildContext(new UpdateBountiesEvent()));

        bounties.Verify(b => b.ClearMonthlyBoard(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeUpdateBountiesDoesNotClearBoardOnOtherDays()
    {
        var bounties = new Mock<IChartBountyRepository>();
        var saga = BuildSaga(bounties: bounties, dateTime: FakeDateTime.At(2026, 5, 15));

        await saga.Consume(BuildContext(new UpdateBountiesEvent()));

        bounties.Verify(b => b.ClearMonthlyBoard(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeUpdateBountiesAssignsTopBountyToChartsWithoutScores()
    {
        var bounties = new Mock<IChartBountyRepository>();
        var charts = new Mock<IChartRepository>();
        var unscoredChartId = Guid.NewGuid();
        // Return one unscored chart for Single+lowest level; everything else empty.
        charts.Setup(c => c.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(1), ChartType.Single,
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Chart(unscoredChartId, MixEnum.Phoenix,
                new Song(Name.From("s"), SongType.Arcade,
                    new Uri("https://example.invalid"), TimeSpan.FromMinutes(2), Name.From("a"), Bpm: null),
                ChartType.Single, DifficultyLevel.From(1), MixEnum.Phoenix, null, null, null,
                new HashSet<Skill>()) });

        var saga = BuildSaga(charts: charts, bounties: bounties, dateTime: FakeDateTime.At(2026, 5, 15));

        await saga.Consume(BuildContext(new UpdateBountiesEvent()));

        // Unscored charts always get bounty 10 (top reward).
        bounties.Verify(b => b.SetChartBounty(unscoredChartId, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumePlayerScoreUpdatedRedeemsZeroWhenNoBountiesMatch()
    {
        var bounties = new Mock<IChartBountyRepository>();
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(Guid.NewGuid(), TotalRating: 0, HighestLevel: 1,
                ClearCount: 0, CoOpRating: 0, CoOpScore: 0, SkillRating: 0, SkillScore: 0, SkillLevel: 0,
                SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0, DoublesRating: 0, DoublesScore: 0,
                DoublesLevel: 0, CompetitiveLevel: 15.0, SinglesCompetitiveLevel: 15.0,
                DoublesCompetitiveLevel: 15.0));
        var saga = BuildSaga(bounties: bounties, stats: stats);

        var userId = Guid.NewGuid();
        await saga.Consume(BuildContext(new PlayerScoreUpdatedEvent(userId,
            NewChartIds: new[] { Guid.NewGuid() },
            UpscoredChartIds: new Dictionary<Guid, int>())));

        bounties.Verify(b => b.RedeemBounty(userId, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumePlayerScoreUpdatedSumsBountyWorthForMatchingCharts()
    {
        var bountyChartId = Guid.NewGuid();
        var bounties = new Mock<IChartBountyRepository>();
        // Default empty for any combination, then specific return for the single Single+15 call.
        bounties.Setup(b => b.GetChartBounties(It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartBounty>());
        bounties.Setup(b => b.GetChartBounties(ChartType.Single, DifficultyLevel.From(15),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChartBounty(bountyChartId, Worth: 5) });
        var stats = new Mock<IPlayerStatsRepository>();
        stats.Setup(s => s.GetStats(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(Guid.NewGuid(), TotalRating: 0, HighestLevel: 1,
                ClearCount: 0, CoOpRating: 0, CoOpScore: 0, SkillRating: 0, SkillScore: 0, SkillLevel: 0,
                SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0, DoublesRating: 0, DoublesScore: 0,
                DoublesLevel: 0, CompetitiveLevel: 15.0, SinglesCompetitiveLevel: 15.0,
                DoublesCompetitiveLevel: 15.0));
        var saga = BuildSaga(bounties: bounties, stats: stats);

        var userId = Guid.NewGuid();
        await saga.Consume(BuildContext(new PlayerScoreUpdatedEvent(userId,
            NewChartIds: new[] { bountyChartId },
            UpscoredChartIds: new Dictionary<Guid, int>())));

        bounties.Verify(b => b.RedeemBounty(userId, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static BountySaga BuildSaga(
        Mock<IPhoenixRecordRepository>? scores = null,
        Mock<IChartRepository>? charts = null,
        Mock<IChartBountyRepository>? bounties = null,
        Mock<IPlayerStatsRepository>? stats = null,
        Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null)
    {
        scores ??= EmptyScoresMock();
        charts ??= EmptyChartsMock();
        bounties ??= new Mock<IChartBountyRepository>();
        stats ??= new Mock<IPlayerStatsRepository>();
        currentUser ??= new Mock<ICurrentUserAccessor>();
        dateTime ??= FakeDateTime.At(2026, 5, 15);
        return new BountySaga(scores.Object, charts.Object, bounties.Object, stats.Object,
            currentUser.Object, dateTime.Object);
    }

    private static Mock<IPhoenixRecordRepository> EmptyScoresMock()
    {
        var m = new Mock<IPhoenixRecordRepository>();
        m.Setup(s => s.GetMeaningfulScoresCount(It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartScoreAggregate>());
        m.Setup(s => s.GetRecordedScores(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        return m;
    }

    private static Mock<IChartRepository> EmptyChartsMock()
    {
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        return m;
    }

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
