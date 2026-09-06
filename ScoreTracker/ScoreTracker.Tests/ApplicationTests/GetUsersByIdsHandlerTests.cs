using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetUsersByIdsHandlerTests
{
    [Fact]
    public async Task ReturnsEveryUserTheRepositoryFindsForTheIds()
    {
        var first = new UserBuilder().WithId(Guid.NewGuid()).Build();
        var second = new UserBuilder().WithId(Guid.NewGuid()).Build();
        var missing = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUsers(It.Is<IEnumerable<Guid>>(ids => ids.Count() == 3), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });
        var handler = new GetUsersByIdsHandler(users.Object);

        var result = await handler.Handle(new GetUsersByIdsQuery(new[] { first.Id, second.Id, missing }),
            CancellationToken.None);

        Assert.Equal(new[] { first, second }, result);
    }

    [Fact]
    public async Task AnEmptyIdSetNeverReachesTheRepository()
    {
        var users = new Mock<IUserRepository>();
        var handler = new GetUsersByIdsHandler(users.Object);

        var result = await handler.Handle(new GetUsersByIdsQuery(Array.Empty<Guid>()), CancellationToken.None);

        Assert.Empty(result);
        users.Verify(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
