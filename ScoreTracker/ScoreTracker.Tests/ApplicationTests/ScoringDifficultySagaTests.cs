using System;
using System.Collections.Generic;
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
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ScoringDifficultySagaTests
{
    private static ScoringDifficultySaga Build(
        Mock<IChartRepository>? charts = null,
        Mock<IPhoenixRecordRepository>? scores = null,
        Mock<IPlayerStatsRepository>? playerStats = null)
    {
        charts ??= new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        scores ??= new Mock<IPhoenixRecordRepository>();
        scores.Setup(s => s.GetAllPlayerScores(It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, RecordedPhoenixScore)>());
        playerStats ??= new Mock<IPlayerStatsRepository>();
        return new ScoringDifficultySaga(charts.Object, scores.Object, playerStats.Object);
    }

    private static ConsumeContext<T> ContextOf<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    [Theory]
    [InlineData(new double[] { 1, 1, 1, 1 }, false, 0)]
    [InlineData(new double[] { 1, 2, 3, 4 }, false, 1.118033988749895)]
    public void StdDevReturnsExpectedPopulationDeviation(double[] values, bool asSample, double expected)
    {
        Assert.Equal(expected, ScoringDifficultySaga.StdDev(values, asSample), 6);
    }

    [Fact]
    public void StdDevAsSampleDiffersFromPopulation()
    {
        double[] values = { 1, 2, 3, 4, 5 };
        var population = ScoringDifficultySaga.StdDev(values, false);
        var sample = ScoringDifficultySaga.StdDev(values, true);
        Assert.True(sample > population);
    }

    [Fact]
    public async Task CalculateChartLetterDifficultiesUpdatesNothingWhenNoChartsExist()
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        var saga = Build(charts: charts);

        await saga.Consume(ContextOf(new CalculateChartLetterDifficultiesEvent()));

        charts.Verify(c => c.UpdateChartLetterDifficulties(It.IsAny<IEnumerable<ChartLetterGradeDifficulty>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CalculateScoringDifficultyDoesNotUpdateScoreLevelsWhenNoScoresExist()
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        var saga = Build(charts: charts);

        await saga.Consume(ContextOf(new CalculateScoringDifficultyEvent()));

        charts.Verify(c => c.UpdateScoreLevel(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<double>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
