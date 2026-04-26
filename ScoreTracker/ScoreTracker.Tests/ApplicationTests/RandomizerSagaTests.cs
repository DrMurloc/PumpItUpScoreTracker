using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RandomizerSagaTests
{
    private static (Mock<ICurrentUserAccessor>, Guid) UserAccessor(bool isLoggedIn = true)
    {
        var userId = Guid.NewGuid();
        var user = new UserBuilder().WithId(userId).Build();
        var accessor = new Mock<ICurrentUserAccessor>();
        accessor.SetupGet(a => a.User).Returns(user);
        accessor.SetupGet(a => a.IsLoggedIn).Returns(isLoggedIn);
        return (accessor, userId);
    }

    private static Mock<IChartRepository> ChartsReturning(IEnumerable<Chart> result)
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return charts;
    }

    private static Mock<IPhoenixRecordRepository> ScoresReturning(Guid userId, IEnumerable<RecordedPhoenixScore> result)
    {
        var scores = new Mock<IPhoenixRecordRepository>();
        scores.Setup(s => s.GetRecordedScores(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return scores;
    }

    private static RandomSettings SinglesAtLevelTwenty(int count = 3)
    {
        var settings = new RandomSettings { Count = count };
        // Level-20 singles get weight 1; everything else stays 0.
        settings.LevelWeights[20] = 1;
        // Arcade song-type weight 1 (test charts default to Arcade).
        settings.SongTypeWeights[SongType.Arcade] = 1;
        return settings;
    }

    [Fact]
    public async Task SaveUserRandomSettingsDelegatesToRepositoryWithCurrentUser()
    {
        var (accessor, userId) = UserAccessor();
        var repo = new Mock<IRandomizerRepository>();
        var saga = new RandomizerSaga(new Mock<IChartRepository>().Object, repo.Object, accessor.Object,
            new Mock<IPhoenixRecordRepository>().Object, new Mock<IRandomNumberGenerator>().Object);

        var settings = new RandomSettings();
        await saga.Handle(new SaveUserRandomSettingsCommand(Name.From("favorites"), settings),
            CancellationToken.None);

        repo.Verify(r => r.SaveSettings(userId, It.Is<Name>(n => (string)n == "favorites"), settings,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteRandomSettingsDelegatesToRepositoryWithCurrentUser()
    {
        var (accessor, userId) = UserAccessor();
        var repo = new Mock<IRandomizerRepository>();
        var saga = new RandomizerSaga(new Mock<IChartRepository>().Object, repo.Object, accessor.Object,
            new Mock<IPhoenixRecordRepository>().Object, new Mock<IRandomNumberGenerator>().Object);

        await saga.Handle(new DeleteRandomSettingsCommand(Name.From("favorites")), CancellationToken.None);

        repo.Verify(r => r.DeleteSettings(userId, It.Is<Name>(n => (string)n == "favorites"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetIncludedRandomChartsReturnsChartsWithNonZeroLevelWeight()
    {
        var (accessor, userId) = UserAccessor();
        var match = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var miss = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var charts = ChartsReturning(new[] { match, miss });
        var saga = new RandomizerSaga(charts.Object, new Mock<IRandomizerRepository>().Object,
            accessor.Object, ScoresReturning(userId, Array.Empty<RecordedPhoenixScore>()).Object,
            new Mock<IRandomNumberGenerator>().Object);

        var result = await saga.Handle(new GetIncludedRandomChartsQuery(SinglesAtLevelTwenty()),
            CancellationToken.None);

        Assert.Equal(new[] { match.Id }, result.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task GetIncludedRandomChartsExcludesChartsWithoutScoringLevelWhenUseScoringLevelsIsTrue()
    {
        var (accessor, userId) = UserAccessor();
        var withScoringLevel = new ChartBuilder().WithLevel(20).WithType(ChartType.Single)
            .WithScoringLevel(20.5).Build();
        var withoutScoringLevel = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var charts = ChartsReturning(new[] { withScoringLevel, withoutScoringLevel });
        var settings = SinglesAtLevelTwenty();
        settings.UseScoringLevels = true;

        var saga = new RandomizerSaga(charts.Object, new Mock<IRandomizerRepository>().Object,
            accessor.Object, ScoresReturning(userId, Array.Empty<RecordedPhoenixScore>()).Object,
            new Mock<IRandomNumberGenerator>().Object);

        var result = await saga.Handle(new GetIncludedRandomChartsQuery(settings), CancellationToken.None);

        Assert.Equal(new[] { withScoringLevel.Id }, result.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task GetIncludedRandomChartsAppliesLetterGradeFilterAgainstUserScores()
    {
        var (accessor, userId) = UserAccessor();
        var pgChart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var aChart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var unscored = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var charts = ChartsReturning(new[] { pgChart, aChart, unscored });

        var settings = SinglesAtLevelTwenty();
        settings.LetterGrades.Add(PhoenixLetterGrade.SSSPlus);

        var scores = ScoresReturning(userId, new[]
        {
            new RecordedPhoenixScore(pgChart.Id, 999000, PhoenixPlate.PerfectGame, false, DateTimeOffset.UtcNow),
            new RecordedPhoenixScore(aChart.Id, 850000, PhoenixPlate.FairGame, false, DateTimeOffset.UtcNow)
        });

        var saga = new RandomizerSaga(charts.Object, new Mock<IRandomizerRepository>().Object,
            accessor.Object, scores.Object, new Mock<IRandomNumberGenerator>().Object);

        var result = await saga.Handle(new GetIncludedRandomChartsQuery(settings), CancellationToken.None);

        Assert.Equal(new[] { pgChart.Id }, result.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task GetIncludedRandomChartsClearStatusTrueRequiresClearedScore()
    {
        var (accessor, userId) = UserAccessor();
        var clearedChart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var brokenChart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var unscoredChart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var charts = ChartsReturning(new[] { clearedChart, brokenChart, unscoredChart });

        var settings = SinglesAtLevelTwenty();
        settings.ClearStatus = true;

        var scores = ScoresReturning(userId, new[]
        {
            new RecordedPhoenixScore(clearedChart.Id, 950000, PhoenixPlate.PerfectGame, false, DateTimeOffset.UtcNow),
            new RecordedPhoenixScore(brokenChart.Id, 950000, PhoenixPlate.PerfectGame, true, DateTimeOffset.UtcNow)
        });

        var saga = new RandomizerSaga(charts.Object, new Mock<IRandomizerRepository>().Object,
            accessor.Object, scores.Object, new Mock<IRandomNumberGenerator>().Object);

        var result = await saga.Handle(new GetIncludedRandomChartsQuery(settings), CancellationToken.None);

        Assert.Equal(new[] { clearedChart.Id }, result.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task GetRandomChartsReturnsRequestedCountUsingWeightedRandomGenerator()
    {
        var (accessor, userId) = UserAccessor();
        var chartA = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var chartB = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var chartC = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var charts = ChartsReturning(new[] { chartA, chartB, chartC });

        var random = new Mock<IRandomNumberGenerator>();
        // Each chart contributes 1 slot to the distribution; sequential picks 0,1,2 → all three.
        random.SetupSequence(r => r.Next(It.IsAny<int>()))
            .Returns(0).Returns(0).Returns(0); // index 0 of remaining each time
        // Final ordering uses NextDouble to randomize; deterministic output keeps order stable.
        random.Setup(r => r.NextDouble()).Returns(0.5);

        var saga = new RandomizerSaga(charts.Object, new Mock<IRandomizerRepository>().Object,
            accessor.Object, ScoresReturning(userId, Array.Empty<RecordedPhoenixScore>()).Object,
            random.Object);

        var settings = SinglesAtLevelTwenty(count: 3);
        var result = (await saga.Handle(new GetRandomChartsQuery(settings), CancellationToken.None)).ToArray();

        Assert.Equal(3, result.Length);
        Assert.Equal(3, result.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public async Task GetRandomChartsReturnsAllIncludedWhenCountExceedsAvailableAndRepeatsDisallowed()
    {
        var (accessor, userId) = UserAccessor();
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var charts = ChartsReturning(new[] { chart });
        var saga = new RandomizerSaga(charts.Object, new Mock<IRandomizerRepository>().Object,
            accessor.Object, ScoresReturning(userId, Array.Empty<RecordedPhoenixScore>()).Object,
            new Mock<IRandomNumberGenerator>().Object);

        var settings = SinglesAtLevelTwenty(count: 5);
        settings.AllowRepeats = false;

        var result = (await saga.Handle(new GetRandomChartsQuery(settings), CancellationToken.None)).ToArray();

        Assert.Single(result);
        Assert.Equal(chart.Id, result[0].Id);
    }

    [Fact]
    public async Task GetRandomChartsOrdersByDecreasingLevelWhenRequested()
    {
        var (accessor, userId) = UserAccessor();
        var low = new ChartBuilder().WithLevel(18).WithType(ChartType.Single).Build();
        var mid = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var high = new ChartBuilder().WithLevel(22).WithType(ChartType.Single).Build();
        var charts = ChartsReturning(new[] { low, mid, high });

        var random = new Mock<IRandomNumberGenerator>();
        random.SetupSequence(r => r.Next(It.IsAny<int>())).Returns(0).Returns(0).Returns(0);

        var settings = new RandomSettings
        {
            Count = 3,
            Ordering = RandomSettings.ResultsOrdering.DecreasingLevel
        };
        settings.LevelWeights[18] = 1;
        settings.LevelWeights[20] = 1;
        settings.LevelWeights[22] = 1;
        settings.SongTypeWeights[SongType.Arcade] = 1;

        var saga = new RandomizerSaga(charts.Object, new Mock<IRandomizerRepository>().Object,
            accessor.Object, ScoresReturning(userId, Array.Empty<RecordedPhoenixScore>()).Object,
            random.Object);

        var result = (await saga.Handle(new GetRandomChartsQuery(settings), CancellationToken.None)).ToArray();

        Assert.Equal(3, result.Length);
        Assert.Equal(new[] { high.Id, mid.Id, low.Id }, result.Select(c => c.Id).ToArray());
    }
}
