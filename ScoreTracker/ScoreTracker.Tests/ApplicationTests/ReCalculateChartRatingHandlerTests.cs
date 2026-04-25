using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ReCalculateChartRatingHandlerTests
{
    [Fact]
    public async Task ClearsAdjustmentAndReturnsBaseWhenNoRatings()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetRatings(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DifficultyAdjustment>());
        var bus = new Mock<IBus>();

        var handler = new ReCalculateChartRatingHandler(ratings.Object, charts.Object, bus.Object);
        var result = await handler.Handle(new ReCalculateChartRatingCommand(MixEnum.Phoenix, chart.Id),
            CancellationToken.None);

        Assert.Equal(chart.Id, result.ChartId);
        Assert.Equal(20.5, result.Difficulty);
        Assert.Equal(0, result.RatingCount);
        ratings.Verify(r => r.ClearAdjustedDifficulty(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        bus.Verify(b => b.Publish(It.IsAny<ChartDifficultyUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PersistsAdjustedDifficultyAndPublishesEventWhenRatingsExist()
    {
        var chart = new ChartBuilder().WithLevel(20).Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetRatings(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { DifficultyAdjustment.Hard, DifficultyAdjustment.VeryHard, DifficultyAdjustment.Hard });
        var bus = new Mock<IBus>();

        var handler = new ReCalculateChartRatingHandler(ratings.Object, charts.Object, bus.Object);
        var result = await handler.Handle(new ReCalculateChartRatingCommand(MixEnum.Phoenix, chart.Id),
            CancellationToken.None);

        Assert.Equal(3, result.RatingCount);
        ratings.Verify(r => r.SetAdjustedDifficulty(MixEnum.Phoenix, chart.Id, It.IsAny<double>(), 3,
            It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(b => b.Publish(
            It.Is<ChartDifficultyUpdatedEvent>(e => e.ChartType == chart.Type && e.Level == (int)chart.Level),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
