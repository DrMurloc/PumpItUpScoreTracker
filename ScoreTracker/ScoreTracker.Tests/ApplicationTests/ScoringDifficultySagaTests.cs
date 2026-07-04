using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Application;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ScoringDifficultySagaTests
{
    private static ScoringDifficultySaga Build(
        Mock<IChartRepository>? charts = null,
        Mock<IScoreReader>? scores = null,
        Mock<IPlayerStatsReader>? playerStats = null,
        Mock<IChartScoringLevelRepository>? scoringLevels = null)
    {
        charts ??= new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        scores ??= new Mock<IScoreReader>();
        scores.Setup(s => s.GetScores(MixEnum.Phoenix, It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, RecordedPhoenixScore)>());
        playerStats ??= new Mock<IPlayerStatsReader>();
        return new ScoringDifficultySaga(charts.Object, scores.Object, playerStats.Object,
            scoringLevels?.Object ?? new Mock<IChartScoringLevelRepository>().Object);
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

        await saga.Consume(ContextOf(new RecalculateChartLetterDifficultiesCommand()));

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
        var scoringLevels = new Mock<IChartScoringLevelRepository>();
        var saga = Build(charts: charts, scoringLevels: scoringLevels);

        await saga.Consume(ContextOf(new RecalculateScoringDifficultyCommand()));

        scoringLevels.Verify(c => c.SaveScoringLevel(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<double?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
