using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
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
        var playerStats = new Mock<IPlayerStatsRepository>();
        var charts = new Mock<IChartRepository>();
        var scores = new Mock<IPhoenixRecordRepository>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        playerStats.Setup(p => p.GetStats(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(userId, singlesCompetitive: 17.5));

        var competitors = new[] { Guid.NewGuid(), Guid.NewGuid() };
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(ChartType.Single, 17.5, .5,
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

        var playerStats = new Mock<IPlayerStatsRepository>();
        var charts = new Mock<IChartRepository>();
        var scores = new Mock<IPhoenixRecordRepository>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        playerStats.Setup(p => p.GetStats(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(userId));
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(ChartType.Single, It.IsAny<double>(), .5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(20), ChartType.Single, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });

        scores.Setup(r => r.GetPlayerScores(It.IsAny<IEnumerable<Guid>>(), ChartType.Single, DifficultyLevel.From(20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, RecordedPhoenixScore)>());

        scores.Setup(r => r.GetRecordedScores(userId, It.IsAny<CancellationToken>()))
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

        var playerStats = new Mock<IPlayerStatsRepository>();
        var charts = new Mock<IChartRepository>();
        var scores = new Mock<IPhoenixRecordRepository>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        playerStats.Setup(p => p.GetStats(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(userId));
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(ChartType.Single, It.IsAny<double>(), .5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { competitor });

        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, DifficultyLevel.From(20), ChartType.Single, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });

        var competitorScore = new RecordedPhoenixScore(chart.Id, 800000, PhoenixPlate.PerfectGame, false,
            DateTimeOffset.UtcNow);
        scores.Setup(r => r.GetPlayerScores(It.IsAny<IEnumerable<Guid>>(), ChartType.Single, DifficultyLevel.From(20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (competitor, competitorScore) });

        scores.Setup(r => r.GetRecordedScores(userId, It.IsAny<CancellationToken>()))
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
}
