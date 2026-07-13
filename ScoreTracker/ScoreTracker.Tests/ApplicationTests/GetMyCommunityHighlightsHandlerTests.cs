using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetMyCommunityHighlightsHandlerTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<ICommunityHighlightRepository> _highlights = new();
    private readonly Mock<IUserReader> _users = new();

    private GetMyCommunityHighlightsHandler Handler() => new(_highlights.Object, _currentUser.Object, _users.Object);

    private void LoggedInAs(Guid userId)
    {
        _currentUser.Setup(c => c.IsLoggedIn).Returns(true);
        _currentUser.Setup(c => c.User).Returns(new UserBuilder().WithId(userId).Build());
    }

    private void RepoReturns(params CommunityHighlightEntry[] entries) =>
        _highlights.Setup(h => h.GetForUser(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Name>>(),
                It.IsAny<MixEnum>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

    private void UsersAre(params User[] users) =>
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

    private static CommunityHighlightEntry Entry(Guid userId) =>
        new(userId, MixEnum.Phoenix, When, SessionId: null,
            new[] { new SignificantWin(WinKind.NotablePg, ChartName: "Bee", RarityShare: 0.004) });

    private static GetMyCommunityHighlightsQuery Query(bool includeOwnWins = true) =>
        new(new Name[] { "Crew" }, MixEnum.Phoenix, includeOwnWins, 30);

    [Fact]
    public async Task ResolvesPlayerNameAvatarAndPublicityForEachEntry()
    {
        var me = Guid.NewGuid();
        var friend = Guid.NewGuid();
        LoggedInAs(me);
        RepoReturns(Entry(friend));
        UsersAre(new UserBuilder().WithId(friend).WithName("kirby").WithIsPublic(true).Build());

        var feed = (await Handler().Handle(Query(), CancellationToken.None)).ToList();

        var record = Assert.Single(feed);
        Assert.Equal(friend, record.UserId);
        Assert.Equal("kirby", record.PlayerName);
        Assert.True(record.IsPublic);
        Assert.Equal(WinKind.NotablePg, record.Wins.Single().Kind);
    }

    [Fact]
    public async Task KeepsOwnWinsWhenTheToggleIsOn()
    {
        var me = Guid.NewGuid();
        var friend = Guid.NewGuid();
        LoggedInAs(me);
        RepoReturns(Entry(me), Entry(friend));
        UsersAre(new UserBuilder().WithId(me).Build(), new UserBuilder().WithId(friend).Build());

        var feed = (await Handler().Handle(Query(includeOwnWins: true), CancellationToken.None)).ToList();

        Assert.Equal(2, feed.Count);
        Assert.Contains(feed, r => r.UserId == me);
    }

    [Fact]
    public async Task DropsOwnWinsWhenTheToggleIsOff()
    {
        var me = Guid.NewGuid();
        var friend = Guid.NewGuid();
        LoggedInAs(me);
        RepoReturns(Entry(me), Entry(friend));
        UsersAre(new UserBuilder().WithId(friend).Build());

        var feed = (await Handler().Handle(Query(includeOwnWins: false), CancellationToken.None)).ToList();

        var record = Assert.Single(feed);
        Assert.Equal(friend, record.UserId);
    }

    [Fact]
    public async Task ReturnsNothingWhenNotLoggedIn()
    {
        _currentUser.Setup(c => c.IsLoggedIn).Returns(false);

        var feed = await Handler().Handle(Query(), CancellationToken.None);

        Assert.Empty(feed);
        _highlights.Verify(h => h.GetForUser(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Name>>(),
            It.IsAny<MixEnum>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
