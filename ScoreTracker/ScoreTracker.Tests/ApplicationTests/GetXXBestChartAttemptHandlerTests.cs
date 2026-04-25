using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetXXBestChartAttemptHandlerTests
{
    [Fact]
    public async Task ReturnsChartAndAttempt()
    {
        var user = new UserBuilder().Build();
        var chartId = Guid.NewGuid();
        var chart = new ChartBuilder().WithId(chartId).Build();
        var attempt = new XXChartAttempt(XXLetterGrade.S, false, 100_000_000,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.XX, chartId, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var attempts = new Mock<IXXChartAttemptRepository>();
        attempts.Setup(a => a.GetBestAttempt(user.Id, chart, It.IsAny<CancellationToken>())).ReturnsAsync(attempt);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetXXBestChartAttemptHandler(currentUser.Object, attempts.Object, charts.Object);
        var result = await handler.Handle(new GetXXBestChartAttemptQuery(chartId), CancellationToken.None);

        Assert.Equal(chart, result.Chart);
        Assert.Equal(attempt, result.BestAttempt);
    }

    [Fact]
    public async Task ReturnsChartWithNullAttemptWhenNoneExists()
    {
        var user = new UserBuilder().Build();
        var chart = new ChartBuilder().Build();
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChart(MixEnum.XX, chart.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chart);
        var attempts = new Mock<IXXChartAttemptRepository>();
        attempts.Setup(a => a.GetBestAttempt(user.Id, chart, It.IsAny<CancellationToken>()))
            .ReturnsAsync((XXChartAttempt?)null);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetXXBestChartAttemptHandler(currentUser.Object, attempts.Object, charts.Object);
        var result = await handler.Handle(new GetXXBestChartAttemptQuery(chart.Id), CancellationToken.None);

        Assert.Null(result.BestAttempt);
    }
}
