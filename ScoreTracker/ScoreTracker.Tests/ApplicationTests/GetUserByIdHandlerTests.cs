using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetUserByIdHandlerTests
{
    [Fact]
    public async Task ReturnsUserWithMatchingId()
    {
        var userId = Guid.NewGuid();
        var user = new UserBuilder().WithId(userId).Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new GetUserByIdHandler(users.Object);
        var result = await handler.Handle(new GetUserByIdQuery(userId), CancellationToken.None);

        Assert.Equal(user, result);
    }

    [Fact]
    public async Task ReturnsNullWhenUserDoesNotExist()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var handler = new GetUserByIdHandler(users.Object);
        var result = await handler.Handle(new GetUserByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
