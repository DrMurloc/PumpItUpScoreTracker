using System;
using System.Linq;
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

public sealed class SearchForUsersHandlerTests
{
    [Fact]
    public async Task ReturnsPublicUsersMatchingByName()
    {
        var publicUser = new UserBuilder().WithName("Alice").WithIsPublic(true).Build();
        var privateUser = new UserBuilder().WithName("Alicia").WithIsPublic(false).Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.SearchForUsersByName("Ali", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { publicUser, privateUser });

        var handler = new SearchForUsersHandler(users.Object);
        var result = await handler.Handle(new SearchForUsersQuery("Ali", 1, 10), CancellationToken.None);

        Assert.Single(result.Results);
        Assert.Contains(publicUser, result.Results);
    }

    [Fact]
    public async Task IncludesPublicUserMatchedByGuidWhenSearchTextIsGuid()
    {
        var matchingId = Guid.NewGuid();
        var matchingUser = new UserBuilder().WithId(matchingId).WithIsPublic(true).Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(matchingId, It.IsAny<CancellationToken>())).ReturnsAsync(matchingUser);
        users.Setup(u => u.SearchForUsersByName(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var handler = new SearchForUsersHandler(users.Object);
        var result = await handler.Handle(new SearchForUsersQuery(matchingId.ToString(), 1, 10),
            CancellationToken.None);

        Assert.Contains(matchingUser, result.Results);
    }

    [Fact]
    public async Task ExcludesPrivateUserMatchedByGuid()
    {
        var matchingId = Guid.NewGuid();
        var privateUser = new UserBuilder().WithId(matchingId).WithIsPublic(false).Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(matchingId, It.IsAny<CancellationToken>())).ReturnsAsync(privateUser);
        users.Setup(u => u.SearchForUsersByName(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var handler = new SearchForUsersHandler(users.Object);
        var result = await handler.Handle(new SearchForUsersQuery(matchingId.ToString(), 1, 10),
            CancellationToken.None);

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task PaginatesResults()
    {
        var page1User = new UserBuilder().WithName("Alice").WithIsPublic(true).Build();
        var page2User = new UserBuilder().WithName("Alicia").WithIsPublic(true).Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.SearchForUsersByName(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { page1User, page2User });

        var handler = new SearchForUsersHandler(users.Object);
        var page2 = await handler.Handle(new SearchForUsersQuery("Ali", Page: 2, Count: 1), CancellationToken.None);

        Assert.Single(page2.Results);
        Assert.Equal(page2User, page2.Results.First());
        Assert.Equal(2, page2.Total);
    }
}
