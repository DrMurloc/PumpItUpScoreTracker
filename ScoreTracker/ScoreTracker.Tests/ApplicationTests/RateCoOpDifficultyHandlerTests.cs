using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RateCoOpDifficultyHandlerTests
{
    [Fact]
    public async Task ThrowsWhenChartIsNotCoOp()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(new UserBuilder().Build());

        var handler = new RateCoOpDifficultyHandler(charts.Object,
            new Mock<IChartDifficultyRatingRepository>().Object, currentUser.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new RateCoOpDifficultyCommand(MixEnum.Phoenix, chart.Id,
                new Dictionary<int, DifficultyLevel> { { 1, DifficultyLevel.From(20) } }),
            CancellationToken.None));
    }

    [Fact]
    public async Task ThrowsWhenPlayerCountMismatch()
    {
        var chart = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(new UserBuilder().Build());

        var handler = new RateCoOpDifficultyHandler(charts.Object,
            new Mock<IChartDifficultyRatingRepository>().Object, currentUser.Object);

        // Chart says 3 players but only 2 ratings supplied
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new RateCoOpDifficultyCommand(MixEnum.Phoenix, chart.Id,
                new Dictionary<int, DifficultyLevel>
                {
                    { 1, DifficultyLevel.From(20) }, { 2, DifficultyLevel.From(20) }
                }),
            CancellationToken.None));
    }

    [Fact]
    public async Task ClearsRatingWhenNoOtherRatersExist()
    {
        var chart = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var user = new UserBuilder().Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetCoOpRatings(chart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, IEnumerable<DifficultyLevel>>());
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new RateCoOpDifficultyHandler(charts.Object, ratings.Object, currentUser.Object);
        var result = await handler.Handle(
            new RateCoOpDifficultyCommand(MixEnum.Phoenix, chart.Id, Ratings: null),
            CancellationToken.None);

        Assert.Null(result);
        ratings.Verify(r => r.ClearCoOpRating(chart.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PersistsAveragedRatingWhenRatingsExist()
    {
        var chart = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build();
        var user = new UserBuilder().Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.Phoenix, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var ratings = new Mock<IChartDifficultyRatingRepository>();
        ratings.Setup(r => r.GetCoOpRatings(chart.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, IEnumerable<DifficultyLevel>>
            {
                { 1, new[] { DifficultyLevel.From(20), DifficultyLevel.From(22) } },
                { 2, new[] { DifficultyLevel.From(18), DifficultyLevel.From(20) } },
                { 3, new[] { DifficultyLevel.From(20), DifficultyLevel.From(20) } }
            });
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new RateCoOpDifficultyHandler(charts.Object, ratings.Object, currentUser.Object);
        var result = await handler.Handle(
            new RateCoOpDifficultyCommand(MixEnum.Phoenix, chart.Id,
                new Dictionary<int, DifficultyLevel>
                {
                    { 1, DifficultyLevel.From(22) },
                    { 2, DifficultyLevel.From(20) },
                    { 3, DifficultyLevel.From(20) }
                }),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(chart.Id, result!.ChartId);
        ratings.Verify(r => r.SaveCoOpRating(It.IsAny<CoOpRating>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
