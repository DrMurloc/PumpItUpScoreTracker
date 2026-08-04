using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Rivals.Application;
using ScoreTracker.Rivals.Contracts.Commands;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RivalSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Mock<IRivalRepository> _rivals = new();
    private readonly Mock<IUserReader> _users = new();

    public RivalSagaTests()
    {
        _currentUser.Setup(c => c.IsLoggedIn).Returns(true);
        _currentUser.Setup(c => c.User).Returns(new UserBuilder().WithId(_me).Build());
        _rivals.Setup(r => r.GetRivalsOwnedBy(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalEdge>());
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>().AsEnumerable());
    }

    private RivalSaga Saga()
    {
        var audience = new RivalAudienceReader(_mediator.Object);
        var adder = new RivalAdder(_rivals.Object, _users.Object, _mediator.Object, audience,
            FakeDateTime.At(Now).Object);
        return new RivalSaga(_rivals.Object, adder, _currentUser.Object, FakeDateTime.At(Now).Object);
    }

    private void TargetIs(Guid userId, bool isPublic) =>
        _users.Setup(u => u.GetUser(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(userId).WithIsPublic(isPublic).Build());

    private void SharesCommunityWith(Guid userId)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CommunityOverviewRecord(Name.From("Crew"), CommunityPrivacyType.Public, 2, false)
            }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityMembersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { userId }.AsEnumerable());
    }

    private void TagResolvesTo(string tag, Guid? linkedUserId) =>
        _mediator.Setup(m => m.Send(It.Is<ResolveOfficialPlayerQuery>(q => q.Tag == tag),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialPlayerResolution(tag, linkedUserId, null, true));

    [Fact]
    public async Task AddingAPublicPlayerWritesTheEdge()
    {
        var target = Guid.NewGuid();
        TargetIs(target, isPublic: true);

        var edgeId = await Saga().Handle(new AddRivalCommand(target, null), CancellationToken.None);

        _rivals.Verify(r => r.Add(It.Is<RivalEdge>(e => e.Id == edgeId && e.OwnerUserId == _me
            && e.TargetUserId == target && e.TargetTag == null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddingAPrivateStrangerIsRefused()
    {
        var target = Guid.NewGuid();
        TargetIs(target, isPublic: false);

        await Assert.ThrowsAsync<RivalNotAvailableException>(() =>
            Saga().Handle(new AddRivalCommand(target, null), CancellationToken.None));
    }

    [Fact]
    public async Task AddingAPrivateClubmateIsAllowed()
    {
        var target = Guid.NewGuid();
        TargetIs(target, isPublic: false);
        SharesCommunityWith(target);

        await Saga().Handle(new AddRivalCommand(target, null), CancellationToken.None);

        _rivals.Verify(r => r.Add(It.IsAny<RivalEdge>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     The normalization rule (D4): a tag that already belongs to an account is stored as the
    ///     ACCOUNT, so the same human can never occupy both columns.
    /// </summary>
    [Fact]
    public async Task ATagThatBelongsToAnAccountIsStoredAsTheAccount()
    {
        var linked = Guid.NewGuid();
        TargetIs(linked, isPublic: true);
        TagResolvesTo("KAZE#4366", linked);

        await Saga().Handle(new AddRivalCommand(null, "KAZE#4366"), CancellationToken.None);

        _rivals.Verify(r => r.Add(It.Is<RivalEdge>(e => e.TargetUserId == linked && e.TargetTag == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Rule 3's exception, arriving as the ordinary privacy rule.</summary>
    [Fact]
    public async Task ATagBelongingToAPrivateAccountIsRefused()
    {
        var linked = Guid.NewGuid();
        TargetIs(linked, isPublic: false);
        TagResolvesTo("HIDDEN#1", linked);

        await Assert.ThrowsAsync<RivalNotAvailableException>(() =>
            Saga().Handle(new AddRivalCommand(null, "HIDDEN#1"), CancellationToken.None));
    }

    [Fact]
    public async Task AnUnlinkedTagIsStoredAsTheTag()
    {
        TagResolvesTo("FRANKEZA#9606", null);

        await Saga().Handle(new AddRivalCommand(null, "FRANKEZA#9606"), CancellationToken.None);

        _rivals.Verify(r => r.Add(It.Is<RivalEdge>(e => e.TargetUserId == null
            && e.TargetTag == "FRANKEZA#9606"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnUnknownTagIsRefused()
    {
        _mediator.Setup(m => m.Send(It.IsAny<ResolveOfficialPlayerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OfficialPlayerResolution?)null);

        await Assert.ThrowsAsync<RivalNotAvailableException>(() =>
            Saga().Handle(new AddRivalCommand(null, "NOBODY#0"), CancellationToken.None));
    }

    [Fact]
    public async Task ABlockRefusesTheAddInEitherDirection()
    {
        var target = Guid.NewGuid();
        TargetIs(target, isPublic: true);
        _rivals.Setup(r => r.IsBlockedEitherWay(_me, target, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<RivalNotAvailableException>(() =>
            Saga().Handle(new AddRivalCommand(target, null), CancellationToken.None));
    }

    [Fact]
    public async Task YouCannotRivalYourself()
    {
        TargetIs(_me, isPublic: true);

        await Assert.ThrowsAsync<RivalNotAvailableException>(() =>
            Saga().Handle(new AddRivalCommand(_me, null), CancellationToken.None));
    }

    /// <summary>Pressing Add twice is a double click, not an error.</summary>
    [Fact]
    public async Task AddingSomebodyYouAlreadyRivalReturnsTheExistingEdge()
    {
        var target = Guid.NewGuid();
        var existing = new RivalEdge(Guid.NewGuid(), _me, target, null, Now);
        TargetIs(target, isPublic: true);
        _rivals.Setup(r => r.GetRivalsOwnedBy(_me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });

        var edgeId = await Saga().Handle(new AddRivalCommand(target, null), CancellationToken.None);

        Assert.Equal(existing.Id, edgeId);
        _rivals.Verify(r => r.Add(It.IsAny<RivalEdge>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemovingWorksFromEitherEndOfTheArrow()
    {
        var theirs = new RivalEdge(Guid.NewGuid(), Guid.NewGuid(), _me, null, Now);
        _rivals.Setup(r => r.GetEdge(theirs.Id, It.IsAny<CancellationToken>())).ReturnsAsync(theirs);

        await Saga().Handle(new RemoveRivalCommand(theirs.Id), CancellationToken.None);

        _rivals.Verify(r => r.Remove(theirs.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemovingSomebodyElsesEdgeIsRefused()
    {
        var strangers = new RivalEdge(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Now);
        _rivals.Setup(r => r.GetEdge(strangers.Id, It.IsAny<CancellationToken>())).ReturnsAsync(strangers);

        await Assert.ThrowsAsync<NotAuthorizedException>(() =>
            Saga().Handle(new RemoveRivalCommand(strangers.Id), CancellationToken.None));
    }

    [Fact]
    public async Task BlockingWritesTheBlock()
    {
        var them = Guid.NewGuid();

        await Saga().Handle(new BlockRivalCommand(them), CancellationToken.None);

        _rivals.Verify(r => r.Block(_me, them, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BlockingYourselfIsRefused()
    {
        await Assert.ThrowsAsync<RivalNotAvailableException>(() =>
            Saga().Handle(new BlockRivalCommand(_me), CancellationToken.None));
    }
}
