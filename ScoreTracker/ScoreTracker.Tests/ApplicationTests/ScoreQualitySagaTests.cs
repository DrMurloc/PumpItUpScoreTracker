using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ScoreQualitySagaTests
{
    private static PlayerStatsRecord Stats(Guid userId, double singlesCompetitive = 20, double doublesCompetitive = 20)
        => new(userId, (Rating)0, DifficultyLevel.From(20), 0, (Rating)0, (PhoenixScore)0, (Rating)0, (PhoenixScore)0,
            0, (Rating)0, (PhoenixScore)0, 0, (Rating)0, (PhoenixScore)0, 0, 0, singlesCompetitive,
            doublesCompetitive);

    private static (Mock<ICurrentUserAccessor>, Guid) UserAccessor()
    {
        var userId = Guid.NewGuid();
        var user = new UserBuilder().WithId(userId).Build();
        var accessor = new Mock<ICurrentUserAccessor>();
        accessor.SetupGet(a => a.User).Returns(user);
        accessor.SetupGet(a => a.IsLoggedIn).Returns(true);
        return (accessor, userId);
    }

    [Fact]
    public async Task GetCompetitivePlayersDelegatesToPlayerStatsRepositoryWithCompetitiveBand()
    {
        var (accessor, userId) = UserAccessor();
        var playerStats = new Mock<IPlayerStatsReader>();
        var charts = new Mock<IChartRepository>();
        var scores = new Mock<IScoreReader>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(userId, singlesCompetitive: 17.5));

        var competitors = new[] { Guid.NewGuid(), Guid.NewGuid() };
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Single, 17.5, .5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(competitors);

        var saga = new ScoreQualitySaga(accessor.Object, playerStats.Object, cache, charts.Object, scores.Object);

        var result = await saga.Handle(new GetCompetitivePlayersQuery(ChartType.Single), CancellationToken.None);

        Assert.Equal(competitors, result);
    }

    [Fact]
    public async Task GetPlayerScoreQualityReturnsTopPercentileWhenNoComparablePlayerScoresThatChart()
    {
        var (accessor, userId) = UserAccessor();
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();

        var playerStats = new Mock<IPlayerStatsReader>();
        var charts = new Mock<IChartRepository>();
        var scores = new Mock<IScoreReader>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(userId));
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Single, It.IsAny<double>(), .5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(20), ChartType.Single, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });

        scores.Setup(r => r.GetPlayerScores(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), ChartType.Single,
                DifficultyLevel.From(20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, RecordedPhoenixScore)>());

        scores.Setup(r => r.GetBestScores(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(chart.Id, 950000, PhoenixPlate.PerfectGame, false, DateTimeOffset.UtcNow)
            });

        var saga = new ScoreQualitySaga(accessor.Object, playerStats.Object, cache, charts.Object, scores.Object);

        var result = await saga.Handle(new GetPlayerScoreQualityQuery(DifficultyLevel.From(20), ChartType.Single),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(1.0, result[chart.Id].Ranking);
        Assert.Equal(1, result[chart.Id].PlayerCount);
    }

    [Fact]
    public async Task GetPlayerScoreQualityReturnsTopPercentileWhenUserBeatsAllComparablePlayers()
    {
        var (accessor, userId) = UserAccessor();
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var competitor = Guid.NewGuid();

        var playerStats = new Mock<IPlayerStatsReader>();
        var charts = new Mock<IChartRepository>();
        var scores = new Mock<IScoreReader>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(userId));
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Single, It.IsAny<double>(), .5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { competitor });

        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(20), ChartType.Single, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });

        var competitorScore = new RecordedPhoenixScore(chart.Id, 800000, PhoenixPlate.PerfectGame, false,
            DateTimeOffset.UtcNow);
        scores.Setup(r => r.GetPlayerScores(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(), ChartType.Single,
                DifficultyLevel.From(20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (competitor, competitorScore) });

        scores.Setup(r => r.GetBestScores(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(chart.Id, 990000, PhoenixPlate.PerfectGame, false, DateTimeOffset.UtcNow)
            });

        var saga = new ScoreQualitySaga(accessor.Object, playerStats.Object, cache, charts.Object, scores.Object);

        var result = await saga.Handle(new GetPlayerScoreQualityQuery(DifficultyLevel.From(20), ChartType.Single),
            CancellationToken.None);

        Assert.Equal(1.0, result[chart.Id].Ranking);
        Assert.Equal(1, result[chart.Id].PlayerCount);
    }

    [Fact]
    public async Task ChartScoreRankingsAreCachedPerChartAcrossUsersInTheSameCompetitiveBucket()
    {
        var (accessor1, userId1) = UserAccessor();
        var (accessor2, userId2) = UserAccessor();
        var playedChart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var unplayedChart = new ChartBuilder().WithLevel(21).WithType(ChartType.Single).Build();
        var competitor = Guid.NewGuid();
        var recordedDate = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        var playerStats = new Mock<IPlayerStatsReader>();
        var charts = new Mock<IChartRepository>();
        var scores = new Mock<IScoreReader>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        // 19.8 and 20.2 both round to the 20.0 half-level bucket.
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(userId1, singlesCompetitive: 19.8));
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(userId2, singlesCompetitive: 20.2));
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Single, 20.0, .5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { competitor });

        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, null, null, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { playedChart, unplayedChart });

        scores.Setup(r => r.GetPlayerScores(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserPhoenixScore(competitor, playedChart.Id, "Competitor", 900000,
                    PhoenixPlate.PerfectGame, false)
            });

        scores.Setup(r => r.GetBestScores(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(playedChart.Id, 950000, PhoenixPlate.PerfectGame, false, recordedDate),
                new RecordedPhoenixScore(unplayedChart.Id, 940000, PhoenixPlate.PerfectGame, false, recordedDate)
            });

        var chartIds = new[] { playedChart.Id, unplayedChart.Id };
        var saga1 = new ScoreQualitySaga(accessor1.Object, playerStats.Object, cache, charts.Object, scores.Object);
        var saga2 = new ScoreQualitySaga(accessor2.Object, playerStats.Object, cache, charts.Object, scores.Object);

        var result1 = await saga1.Handle(new GetChartScoreRankingsQuery(chartIds), CancellationToken.None);
        var result2 = await saga2.Handle(new GetChartScoreRankingsQuery(chartIds), CancellationToken.None);

        Assert.Equal(1.0, result1[playedChart.Id].Ranking);
        Assert.Equal(1, result1[playedChart.Id].PlayerCount);
        Assert.Equal(1.0, result1[unplayedChart.Id].Ranking);
        Assert.Equal(result1[playedChart.Id], result2[playedChart.Id]);
        Assert.Equal(result1[unplayedChart.Id], result2[unplayedChart.Id]);

        // The second user's rankings — including the chart nobody in the cohort has
        // played — must come from cache, not a second ledger query.
        scores.Verify(r => r.GetPlayerScores(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
