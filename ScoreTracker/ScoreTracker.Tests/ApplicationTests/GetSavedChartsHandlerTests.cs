using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetSavedChartsHandlerTests
{
    [Fact]
    public async Task DelegatesToRepositoryForCurrentUser()
    {
        var user = new UserBuilder().Build();
        var saved = new List<SavedChartRecord>();
        var lists = new Mock<IChartListRepository>();
        lists.Setup(l => l.GetSavedChartsByUser(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetSavedChartsHandler(currentUser.Object, lists.Object);
        var result = await handler.Handle(new GetSavedChartsQuery(), CancellationToken.None);

        Assert.Same(saved, result);
    }
}
