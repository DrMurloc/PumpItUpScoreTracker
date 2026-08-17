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

public sealed class GetPlayersVisibilityHandlerTests
{
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPlayerVisibilityReader> _visibility = new();

    public GetPlayersVisibilityHandlerTests()
    {
        _currentUser.Setup(c => c.IsLoggedIn).Returns(true);
        _currentUser.Setup(c => c.User).Returns(new UserBuilder().WithId(_me).Build());
    }

    private GetPlayersVisibilityHandler Handler() => new(_users.Object, _visibility.Object, _currentUser.Object);

    [Fact]
    public async Task EachKnownPlayerIsDescribedOnTheCallersBasesAndUnknownIdsAreAbsent()
    {
        var mate = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var missing = Guid.NewGuid();
        _visibility.Setup(v => v.GetAudience(_me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerAudience(_me,
                new Dictionary<Guid, IReadOnlyList<Name>> { [mate] = new[] { Name.From("Crew") } },
                new HashSet<Guid> { stranger }));
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserBuilder().WithId(mate).WithIsPublic(false).Build(),
                new UserBuilder().WithId(stranger).WithIsPublic(true).Build()
            });

        var described = await Handler().Handle(new GetPlayersVisibilityQuery(new[] { mate, stranger, missing }),
            CancellationToken.None);

        Assert.Equal(new[] { Name.From("Crew") }, described[mate].SharedCommunities);
        Assert.True(described[stranger].IsYourRival);
        Assert.False(described.ContainsKey(missing));
    }

    [Fact]
    public async Task NoIdsAsksNothing()
    {
        var described = await Handler().Handle(new GetPlayersVisibilityQuery(Array.Empty<Guid>()), CancellationToken.None);

        Assert.Empty(described);
        _visibility.Verify(v => v.GetAudience(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
