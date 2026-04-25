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

public sealed class SaveChartToListHandlerTests
{
    [Fact]
    public async Task SavesChartForCurrentUser()
    {
        var user = new UserBuilder().Build();
        var chartId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);
        var lists = new Mock<IChartListRepository>();

        var handler = new SaveChartToListHandler(currentUser.Object, lists.Object);
        await handler.Handle(new SaveChartToListCommand(ChartListType.ToDo, chartId), CancellationToken.None);

        lists.Verify(l => l.SaveChart(user.Id, ChartListType.ToDo, chartId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
