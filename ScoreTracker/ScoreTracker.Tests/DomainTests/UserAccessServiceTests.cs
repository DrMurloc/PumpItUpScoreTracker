using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class UserAccessServiceTests
{
    [Fact]
    public async Task LoggedInUserHasAccessToOwnProfile()
    {
        var userId = Guid.NewGuid();
        var currentUser = new UserBuilder().WithId(userId).Build();
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(c => c.IsLoggedIn).Returns(true);
        currentUserAccessor.SetupGet(c => c.User).Returns(currentUser);
        var users = new Mock<IUserRepository>();

        var service = new UserAccessService(currentUserAccessor.Object, users.Object);

        Assert.True(await service.HasAccessTo(userId));
        users.Verify(u => u.GetUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoggedInUserHasAccessToPublicProfile()
    {
        var currentUser = new UserBuilder().Build();
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(c => c.IsLoggedIn).Returns(true);
        currentUserAccessor.SetupGet(c => c.User).Returns(currentUser);

        var targetId = Guid.NewGuid();
        var target = new UserBuilder().WithId(targetId).WithIsPublic(true).Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(targetId, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var service = new UserAccessService(currentUserAccessor.Object, users.Object);

        Assert.True(await service.HasAccessTo(targetId));
    }

    [Fact]
    public async Task LoggedInUserDeniedFromPrivateProfile()
    {
        var currentUser = new UserBuilder().Build();
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(c => c.IsLoggedIn).Returns(true);
        currentUserAccessor.SetupGet(c => c.User).Returns(currentUser);

        var targetId = Guid.NewGuid();
        var target = new UserBuilder().WithId(targetId).WithIsPublic(false).Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(targetId, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var service = new UserAccessService(currentUserAccessor.Object, users.Object);

        Assert.False(await service.HasAccessTo(targetId));
    }

    [Fact]
    public async Task AnonymousUserHasAccessToPublicProfile()
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(c => c.IsLoggedIn).Returns(false);

        var targetId = Guid.NewGuid();
        var target = new UserBuilder().WithId(targetId).WithIsPublic(true).Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(targetId, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var service = new UserAccessService(currentUserAccessor.Object, users.Object);

        Assert.True(await service.HasAccessTo(targetId));
    }

    [Fact]
    public async Task AnonymousUserDeniedFromPrivateProfile()
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(c => c.IsLoggedIn).Returns(false);

        var targetId = Guid.NewGuid();
        var target = new UserBuilder().WithId(targetId).WithIsPublic(false).Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(targetId, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var service = new UserAccessService(currentUserAccessor.Object, users.Object);

        Assert.False(await service.HasAccessTo(targetId));
    }

    [Fact]
    public async Task DeniedWhenTargetUserDoesNotExist()
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>();
        currentUserAccessor.SetupGet(c => c.IsLoggedIn).Returns(false);

        var targetId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(targetId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var service = new UserAccessService(currentUserAccessor.Object, users.Object);

        Assert.False(await service.HasAccessTo(targetId));
    }
}
