using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetXXBestChartAttemptsHandlerTests
{
    [Fact]
    public async Task ReturnsEmptyWhenAccessIsDenied()
    {
        var userId = Guid.NewGuid();
        var access = new Mock<IUserAccessService>();
        access.Setup(a => a.HasAccessTo(userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var attempts = new Mock<IXXChartAttemptRepository>();

        var handler = new GetXXBestChartAttemptsHandler(attempts.Object, access.Object);
        var result = await handler.Handle(new GetXXBestChartAttemptsQuery(userId), CancellationToken.None);

        Assert.Empty(result);
        attempts.Verify(a => a.GetBestAttempts(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReturnsAttemptsWhenAccessGranted()
    {
        var userId = Guid.NewGuid();
        var attemptList = new List<BestXXChartAttempt>();
        var access = new Mock<IUserAccessService>();
        access.Setup(a => a.HasAccessTo(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var attempts = new Mock<IXXChartAttemptRepository>();
        attempts.Setup(a => a.GetBestAttempts(userId, It.IsAny<CancellationToken>())).ReturnsAsync(attemptList);

        var handler = new GetXXBestChartAttemptsHandler(attempts.Object, access.Object);
        var result = await handler.Handle(new GetXXBestChartAttemptsQuery(userId), CancellationToken.None);

        Assert.Same(attemptList, result);
    }
}
