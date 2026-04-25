using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ApiTokenHandlerTests
{
    [Fact]
    public async Task GetUserApiTokenReturnsTokenForCurrentUser()
    {
        var user = new UserBuilder().Build();
        var token = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserApiToken(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(token);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new ApiTokenHandler(users.Object, currentUser.Object);
        var result = await handler.Handle(new GetUserApiTokenQuery(), CancellationToken.None);

        Assert.Equal(token, result);
    }

    [Fact]
    public async Task GetUserByApiTokenDelegatesToRepository()
    {
        var user = new UserBuilder().Build();
        var token = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserByApiToken(token, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new ApiTokenHandler(users.Object, new Mock<ICurrentUserAccessor>().Object);
        var result = await handler.Handle(new GetUserByApiTokenQuery(token), CancellationToken.None);

        Assert.Equal(user, result);
    }

    [Fact]
    public async Task SetApiTokenWritesNewGuidForCurrentUserAndReturnsIt()
    {
        var user = new UserBuilder().Build();
        var users = new Mock<IUserRepository>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);

        var handler = new ApiTokenHandler(users.Object, currentUser.Object);
        var result = await handler.Handle(new SetApiTokenCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
        users.Verify(u => u.SetUserApiToken(user.Id, result, It.IsAny<CancellationToken>()), Times.Once);
    }
}
