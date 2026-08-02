using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetMyCommunityHighlightsHandlerTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<ICommunityHighlightRepository> _index = new();
    private readonly Mock<IMediator> _mediator = new();

    private GetMyCommunityHighlightsHandler Handler() =>
        new(_index.Object, _currentUser.Object, _mediator.Object);

    private void LoggedInAs(Guid userId)
    {
        _currentUser.Setup(c => c.IsLoggedIn).Returns(true);
        _currentUser.Setup(c => c.User).Returns(new UserBuilder().WithId(userId).Build());
    }

    private void IndexReturns(params Guid[] eventIds) =>
        _index.Setup(h => h.GetVisibleEventIds(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Name>>(),
                It.IsAny<MixEnum>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventIds);

    private void PayloadsAre(params PlayerHighlightRecord[] records) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayerHighlightsForEventsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

    private static PlayerHighlightRecord Record(Guid eventId, Guid userId, string name = "kirby",
        bool isPublic = true) =>
        new(eventId, userId, name, new Uri("https://piu.test/a.png"), isPublic, MixEnum.Phoenix, When,
            SessionId: null,
            new[] { new SignificantWin(WinKind.NotablePg, ChartName: "Bee", RarityShare: 0.004) });

    private static GetMyCommunityHighlightsQuery Query(bool includeOwnWins = true) =>
        new(new Name[] { "Crew" }, MixEnum.Phoenix, includeOwnWins, 30);

    [Fact]
    public async Task ReturnsThePayloadForEveryVisibleEvent()
    {
        var me = Guid.NewGuid();
        var friend = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        LoggedInAs(me);
        IndexReturns(eventId);
        PayloadsAre(Record(eventId, friend));

        var feed = (await Handler().Handle(Query(), CancellationToken.None)).ToList();

        var record = Assert.Single(feed);
        Assert.Equal(friend, record.UserId);
        Assert.Equal("kirby", record.PlayerName);
        Assert.True(record.IsPublic);
        Assert.Equal(WinKind.NotablePg, record.Wins.Single().Kind);
    }

    /// <summary>
    ///     The index decides the order, and the payload query is free to answer in any order at
    ///     all — so the handler has to re-impose it rather than trust what comes back.
    /// </summary>
    [Fact]
    public async Task KeepsTheIndexsOrderRatherThanThePayloadQuerys()
    {
        var me = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        LoggedInAs(me);
        IndexReturns(first, second);
        PayloadsAre(Record(second, Guid.NewGuid()), Record(first, Guid.NewGuid()));

        var feed = (await Handler().Handle(Query(), CancellationToken.None)).ToList();

        Assert.Equal(new[] { first, second }, feed.Select(r => r.EventId));
    }

    /// <summary>
    ///     Retention lives on the payload, so an index row can outlive the wins it points at for
    ///     as long as it takes the next purge to run. That row is a gap, not a crash.
    /// </summary>
    [Fact]
    public async Task DropsEventsWhosePayloadHasAgedOut()
    {
        var me = Guid.NewGuid();
        var alive = Guid.NewGuid();
        var expired = Guid.NewGuid();
        LoggedInAs(me);
        IndexReturns(expired, alive);
        PayloadsAre(Record(alive, Guid.NewGuid()));

        var feed = (await Handler().Handle(Query(), CancellationToken.None)).ToList();

        Assert.Equal(alive, Assert.Single(feed).EventId);
    }

    [Fact]
    public async Task KeepsOwnWinsWhenTheToggleIsOn()
    {
        var me = Guid.NewGuid();
        var friend = Guid.NewGuid();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        LoggedInAs(me);
        IndexReturns(mine, theirs);
        PayloadsAre(Record(mine, me), Record(theirs, friend));

        var feed = (await Handler().Handle(Query(includeOwnWins: true), CancellationToken.None)).ToList();

        Assert.Equal(2, feed.Count);
        Assert.Contains(feed, r => r.UserId == me);
    }

    [Fact]
    public async Task DropsOwnWinsWhenTheToggleIsOff()
    {
        var me = Guid.NewGuid();
        var friend = Guid.NewGuid();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        LoggedInAs(me);
        IndexReturns(mine, theirs);
        PayloadsAre(Record(mine, me), Record(theirs, friend));

        var feed = (await Handler().Handle(Query(includeOwnWins: false), CancellationToken.None)).ToList();

        Assert.Equal(friend, Assert.Single(feed).UserId);
    }

    [Fact]
    public async Task ReturnsNothingWhenNotLoggedIn()
    {
        _currentUser.Setup(c => c.IsLoggedIn).Returns(false);

        var feed = await Handler().Handle(Query(), CancellationToken.None);

        Assert.Empty(feed);
        _index.Verify(h => h.GetVisibleEventIds(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Name>>(),
            It.IsAny<MixEnum>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
