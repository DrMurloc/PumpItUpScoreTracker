using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetUserByDiscordIdHandlerTests
{
    [Fact]
    public async Task DelegatesToRepository()
    {
        var user = new UserBuilder().Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUserByExternalLogin("Discord", "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new GetUserByDiscordIdHandler(users.Object);
        var result = await handler.Handle(new GetUserByExternalLoginQuery("abc123", "Discord"), CancellationToken.None);

        Assert.Equal(user, result);
    }
}
