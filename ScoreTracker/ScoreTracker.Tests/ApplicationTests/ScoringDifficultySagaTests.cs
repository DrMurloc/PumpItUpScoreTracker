using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.Tests.TestData;
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
        // Defaults are for the ports a test did not bring — a caller that passes its own mock
        // has already said what it wants back, and stubbing over it would silently win.
        if (charts is null)
        {
            charts = new Mock<IChartRepository>();
            charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                    It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Chart>());
        }

        if (scores is null)
        {
            scores = new Mock<IScoreReader>();
            scores.Setup(s => s.GetScores(MixEnum.Phoenix, It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<(Guid, RecordedPhoenixScore)>());
        }

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

    [Fact]
    public async Task AnUnmeasuredChartScoresAtItsListedLevel()
    {
        // ~13% of the competitive range has no measured scoring level. Returning nothing made
        // every caller invent its own answer; the listed level is what the chart claims about
        // itself, so it is the honest prior until scores disagree.
        var measured = new ChartBuilder().WithLevel(23).Build();
        var unmeasured = new ChartBuilder().WithLevel(21).Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { measured, unmeasured });
        var scoringLevels = new Mock<IChartScoringLevelRepository>();
        scoringLevels.Setup(s => s.GetScoringLevels(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double> { [measured.Id] = 22.3 });

        var result = await Build(charts: charts, scoringLevels: scoringLevels)
            .Handle(new GetChartScoringLevelsQuery(), CancellationToken.None);

        // The measured one keeps its measurement — the fallback fills gaps, it never overwrites.
        Assert.Equal(22.3, result[measured.Id]);
        Assert.Equal(21, result[unmeasured.Id]);
    }
}
