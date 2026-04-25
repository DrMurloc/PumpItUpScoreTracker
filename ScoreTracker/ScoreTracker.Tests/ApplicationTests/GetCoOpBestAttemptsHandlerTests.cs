using System.Collections.Generic;
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

public sealed class GetCoOpBestAttemptsHandlerTests
{
    [Fact]
    public async Task FetchesCoOpChartsThenBestAttemptsForCurrentUser()
    {
        var user = new UserBuilder().Build();
        var coOpCharts = new[] { new ChartBuilder().WithType(ChartType.CoOp).WithLevel(3).Build() };
        var attempts = new List<BestXXChartAttempt>();

        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCoOpCharts(MixEnum.XX, It.IsAny<CancellationToken>())).ReturnsAsync(coOpCharts);
        var attemptRepo = new Mock<IXXChartAttemptRepository>();
        attemptRepo.Setup(a => a.GetBestAttempts(user.Id, coOpCharts, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempts);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetCoOpBestAttemptsHandler(attemptRepo.Object, charts.Object, currentUser.Object);
        var result = await handler.Handle(new GetXXCoOpBestAttemptsQuery(), CancellationToken.None);

        Assert.Same(attempts, result);
    }
}
