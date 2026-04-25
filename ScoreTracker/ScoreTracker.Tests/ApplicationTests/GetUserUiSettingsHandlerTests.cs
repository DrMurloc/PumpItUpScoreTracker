using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetUserUiSettingsHandlerTests
{
    [Fact]
    public async Task UsesCurrentUserWhenQueryUserIdIsNull()
    {
        var user = new UserBuilder().Build();
        var settings = new Dictionary<string, string> { { "theme", "dark" } };
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserUiSettings(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new GetUserUiSettingsHandler(users.Object, currentUser.Object);
        var result = await handler.Handle(new GetUserUiSettingsQuery(), CancellationToken.None);

        Assert.Same(settings, result);
    }

    [Fact]
    public async Task UsesQueryUserIdWhenProvided()
    {
        var overrideId = Guid.NewGuid();
        var settings = new Dictionary<string, string>();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserUiSettings(overrideId, It.IsAny<CancellationToken>())).ReturnsAsync(settings);

        var handler = new GetUserUiSettingsHandler(users.Object, new Mock<ICurrentUserAccessor>().Object);
        var result = await handler.Handle(new GetUserUiSettingsQuery(overrideId), CancellationToken.None);

        Assert.Same(settings, result);
    }
}
