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
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SearchPlayersHandlerTests
{
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPlayerVisibilityReader> _visibility = new();

    public SearchPlayersHandlerTests()
    {
        _currentUser.Setup(c => c.IsLoggedIn).Returns(true);
        _currentUser.Setup(c => c.User).Returns(new UserBuilder().WithId(_me).Build());
        _visibility.Setup(v => v.GetAudience(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlayerAudience.Anonymous);
        _users.Setup(u => u.SearchVisibleUsers(It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
    }

    private SearchPlayersHandler Handler() => new(_users.Object, _visibility.Object, _currentUser.Object);

    [Fact]
    public async Task ABlankTermAsksNothingOfTheDatabase()
    {
        var hits = await Handler().Handle(new SearchPlayersQuery("   "), CancellationToken.None);

        Assert.Empty(hits);
        _users.Verify(u => u.SearchVisibleUsers(It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheAudienceIsTheRepositorysPrivateAllowance()
    {
        var mate = Guid.NewGuid();
        var rival = Guid.NewGuid();
        _visibility.Setup(v => v.GetAudience(_me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerAudience(_me,
                new Dictionary<Guid, IReadOnlyList<Name>> { [mate] = new[] { Name.From("Crew") } },
                new HashSet<Guid> { rival }));

        await Handler().Handle(new SearchPlayersQuery("ro", 4), CancellationToken.None);

        _users.Verify(u => u.SearchVisibleUsers("ro", 4,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(_me) && ids.Contains(mate) && ids.Contains(rival)
                                                    && ids.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EachHitCarriesTheBasisItWasSeenOn()
    {
        var mate = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        _visibility.Setup(v => v.GetAudience(_me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerAudience(_me,
                new Dictionary<Guid, IReadOnlyList<Name>> { [mate] = new[] { Name.From("Crew") } },
                new HashSet<Guid>()));
        _users.Setup(u => u.SearchVisibleUsers("ro", It.IsAny<int>(), It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserBuilder().WithId(mate).WithName("Robby").WithIsPublic(false).Build(),
                new UserBuilder().WithId(stranger).WithName("Roxy").WithIsPublic(true).Build()
            });

        var hits = await Handler().Handle(new SearchPlayersQuery("ro"), CancellationToken.None);

        Assert.Equal(new[] { Name.From("Crew") }, hits.Single(h => h.UserId == mate).Visibility.SharedCommunities);
        Assert.True(hits.Single(h => h.UserId == stranger).Visibility.IsPublic);
        Assert.Empty(hits.Single(h => h.UserId == stranger).Visibility.SharedCommunities);
    }

    [Fact]
    public async Task AnonymousSearchesWithNoAllowance()
    {
        _currentUser.Setup(c => c.IsLoggedIn).Returns(false);

        await Handler().Handle(new SearchPlayersQuery("d"), CancellationToken.None);

        _visibility.Verify(v => v.GetAudience(null, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.SearchVisibleUsers("d", It.IsAny<int>(),
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }
}
