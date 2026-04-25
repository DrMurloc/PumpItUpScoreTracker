using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RemoveChartFromListHandlerTests
{
    [Fact]
    public async Task RemovesChartForCurrentUser()
    {
        var user = new UserBuilder().Build();
        var chartId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);
        var lists = new Mock<IChartListRepository>();

        var handler = new RemoveChartFromListHandler(currentUser.Object, lists.Object);
        await handler.Handle(new RemoveChartFromListCommand(ChartListType.Favorite, chartId), CancellationToken.None);

        lists.Verify(l => l.RemoveChart(user.Id, ChartListType.Favorite, chartId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
