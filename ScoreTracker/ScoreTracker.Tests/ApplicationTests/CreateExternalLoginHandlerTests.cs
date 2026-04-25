using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CreateExternalLoginHandlerTests
{
    [Fact]
    public async Task CreatesLoginWhenNoneExists()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserByExternalLogin("Discord", "ext-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Models.User?)null);

        var handler = new CreateExternalLoginHandler(users.Object);
        var userId = Guid.NewGuid();
        await handler.Handle(new CreateExternalLoginCommand(userId, "ext-1", "Discord"), CancellationToken.None);

        users.Verify(u => u.CreateExternalLogin(userId, "Discord", "ext-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ThrowsWhenLoginAlreadyExists()
    {
        var existing = new UserBuilder().Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserByExternalLogin("Discord", "ext-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new CreateExternalLoginHandler(users.Object);

        await Assert.ThrowsAsync<Exception>(() =>
            handler.Handle(new CreateExternalLoginCommand(Guid.NewGuid(), "ext-1", "Discord"), CancellationToken.None));

        users.Verify(u => u.CreateExternalLogin(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
