using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommunitySagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleCreateCommunityThrowsWhenCommunityNameAlreadyExists()
    {
        var ctx = new HandlerContext();
        var existing = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.Public, false);
        ctx.Communities.Setup(c => c.GetCommunityByName(It.Is<Name>(n => (string)n == "Acme"),
            It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        await Assert.ThrowsAsync<CommunityAlreadyExistsException>(() =>
            ctx.Saga.Handle(new CreateCommunityCommand(Name.From("Acme"), CommunityPrivacyType.Public),
                CancellationToken.None));
    }

    [Fact]
    public async Task HandleCreateCommunitySavesNewCommunityWithCurrentUserAsOwnerAndMember()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(currentUserId: userId);

        await ctx.Saga.Handle(new CreateCommunityCommand(Name.From("Acme"), CommunityPrivacyType.Public),
            CancellationToken.None);

        ctx.Communities.Verify(c => c.SaveCommunity(
            It.Is<Community>(comm => (string)comm.Name == "Acme" && comm.OwnerId == userId
                                     && comm.MemberIds.Contains(userId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleJoinCommunityIsIdempotentForExistingMembers()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(currentUserId: userId);
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.Public,
            new[] { userId }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false);
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        await ctx.Saga.Handle(new JoinCommunityCommand(Name.From("Acme"), null), CancellationToken.None);

        ctx.Communities.Verify(c => c.SaveCommunity(It.IsAny<Community>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleJoinCommunityAddsMemberToPublicCommunity()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(currentUserId: userId);
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.Public, false);
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        await ctx.Saga.Handle(new JoinCommunityCommand(Name.From("Acme"), null), CancellationToken.None);

        ctx.Communities.Verify(c => c.SaveCommunity(
            It.Is<Community>(comm => comm.MemberIds.Contains(userId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleJoinCommunityThrowsWhenPrivateAndNoInviteCodeProvided()
    {
        var ctx = new HandlerContext();
        var community = new Community(Name.From("Secret"), Guid.NewGuid(), CommunityPrivacyType.Private, false);
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        await Assert.ThrowsAsync<DeniedFromCommunityException>(() =>
            ctx.Saga.Handle(new JoinCommunityCommand(Name.From("Secret"), InviteCode: null),
                CancellationToken.None));
    }

    [Fact]
    public async Task HandleLeaveCommunityRemovesMemberFromTheSet()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(currentUserId: userId);
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.Public,
            new[] { userId }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false);
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        await ctx.Saga.Handle(new LeaveCommunityCommand(Name.From("Acme")), CancellationToken.None);

        ctx.Communities.Verify(c => c.SaveCommunity(
            It.Is<Community>(comm => !comm.MemberIds.Contains(userId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleCreateInviteLinkThrowsWhenCallerIsNotAMember()
    {
        var callerId = Guid.NewGuid();
        var ctx = new HandlerContext(currentUserId: callerId);
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.Public, false);
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        await Assert.ThrowsAsync<DeniedFromCommunityException>(() =>
            ctx.Saga.Handle(new CreateInviteLinkCommand(Name.From("Acme"), ExpirationDate: null),
                CancellationToken.None));
    }

    [Fact]
    public async Task HandleCreateInviteLinkPersistsTheCodeAndReturnsIt()
    {
        var memberId = Guid.NewGuid();
        var ctx = new HandlerContext(currentUserId: memberId);
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.Public,
            new[] { memberId }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?>(), false);
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        var code = await ctx.Saga.Handle(new CreateInviteLinkCommand(Name.From("Acme"), ExpirationDate: null),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, code);
        ctx.Communities.Verify(c => c.SaveCommunity(
            It.Is<Community>(comm => comm.InviteCodes.ContainsKey(code)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleGetMyCommunitiesReturnsPersonalListWhenLoggedInElsePublicList(bool loggedIn)
    {
        var ctx = new HandlerContext(currentUserId: Guid.NewGuid(), isLoggedIn: loggedIn);

        await ctx.Saga.Handle(new GetMyCommunitiesQuery(), CancellationToken.None);

        if (loggedIn)
        {
            ctx.Communities.Verify(c => c.GetCommunities(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Once);
            ctx.Communities.Verify(c => c.GetPublicCommunities(It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            ctx.Communities.Verify(c => c.GetPublicCommunities(It.IsAny<CancellationToken>()), Times.Once);
            ctx.Communities.Verify(c => c.GetCommunities(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    [Fact]
    public async Task HandleGetCommunityHidesPrivateCommunityFromNonMembers()
    {
        var ctx = new HandlerContext(currentUserId: Guid.NewGuid(), isLoggedIn: true);
        var privateCommunity = new Community(Name.From("Secret"), Guid.NewGuid(),
            CommunityPrivacyType.Private, false);
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateCommunity);

        await Assert.ThrowsAsync<CommunityNotFoundException>(() =>
            ctx.Saga.Handle(new GetCommunityQuery(Name.From("Secret")), CancellationToken.None));
    }

    [Fact]
    public async Task ConsumeNewTitlesAcquiredBroadcastsTitleListToCommunityChannels()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);

        await ctx.Saga.Consume(BuildContext(new NewTitlesAcquiredEvent(userId,
            NewTitles: new[] { "First Title", "Second Title" },
            ParagonUpgrades: new Dictionary<string, string>())));

        ctx.Bot.Verify(b => b.SendMessages(
            It.Is<IEnumerable<string>>(msgs => msgs.Any(m => m.Contains("alice") && m.Contains("First Title"))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ConsumePlayerRatingsImprovedDoesNothingWhenUserNotFound()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.Users.Setup(u => u.GetUser(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await ctx.Saga.Consume(BuildContext(BuildRatingsImprovedAllSame(userId)));

        ctx.Bot.Verify(b => b.SendMessages(It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<ulong>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumePlayerRatingsImprovedSkipsBroadcastWhenNothingActuallyImproved()
    {
        // All "new" values equal "old" values → no parts of the message are appended → no send.
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, "Acme", channelId: 12345);

        await ctx.Saga.Consume(BuildContext(BuildRatingsImprovedAllSame(userId)));

        ctx.Bot.Verify(b => b.SendMessages(It.IsAny<IEnumerable<string>>(),
            It.IsAny<IEnumerable<ulong>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeUserUpdatedJoinsWorldAndCountryWhenPublic()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();

        await ctx.Saga.Consume(BuildContext(new UserUpdatedEvent(userId, Country: "USA", IsPublic: true)));

        ctx.Mediator.Verify(m => m.Send(
            It.Is<JoinCommunityCommand>(j => (string)j.CommunityName == "World" && j.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(
            It.Is<JoinCommunityCommand>(j => (string)j.CommunityName == "USA" && j.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(It.IsAny<LeaveCommunityCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConsumeUserUpdatedLeavesWorldWhenNoLongerPublic()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();

        await ctx.Saga.Consume(BuildContext(new UserUpdatedEvent(userId, Country: null, IsPublic: false)));

        ctx.Mediator.Verify(m => m.Send(
            It.Is<LeaveCommunityCommand>(l => (string)l.CommunityName == "World" && l.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(It.IsAny<JoinCommunityCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class HandlerContext
    {
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<ICommunityRepository> Communities { get; } = new();
        public Mock<IBotClient> Bot { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IPhoenixRecordRepository> Scores { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IUcsRepository> Ucs { get; } = new();
        public Mock<IDateTimeOffsetAccessor> DateTime { get; } = FakeDateTime.At(Now);
        public CommunitySaga Saga { get; }

        public HandlerContext(Guid? currentUserId = null, bool isLoggedIn = true)
        {
            var id = currentUserId ?? Guid.NewGuid();
            CurrentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(id).Build());
            CurrentUser.SetupGet(u => u.IsLoggedIn).Returns(isLoggedIn);
            Communities.Setup(c => c.GetCommunities(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
            Saga = new CommunitySaga(CurrentUser.Object, Communities.Object, Bot.Object, Users.Object,
                Charts.Object, Scores.Object, Mediator.Object, Ucs.Object, DateTime.Object);
        }

        public void GivenUser(Guid userId, string name)
        {
            Users.Setup(u => u.GetUser(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserBuilder().WithId(userId).WithName(name).Build());
        }

        public void GivenUserCommunitiesWithChannel(Guid userId, string communityName, ulong channelId)
        {
            Communities.Setup(c => c.GetCommunities(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new CommunityOverviewRecord(Name.From(communityName), CommunityPrivacyType.Public,
                        MemberCount: 1, IsRegional: false)
                });
            Communities.Setup(c => c.GetCommunityByName(It.Is<Name>(n => (string)n == communityName),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Community(Name.From(communityName), Guid.NewGuid(),
                    CommunityPrivacyType.Public,
                    new[] { userId },
                    new[]
                    {
                        new Community.ChannelConfiguration(channelId, SendNewScores: true,
                            SendTitles: true, SendNewMembers: true)
                    },
                    new Dictionary<Guid, DateOnly?>(), false));
        }
    }

    private static PlayerRatingsImprovedEvent BuildRatingsImprovedAllSame(Guid userId) =>
        new(userId, OldTop50: 100, OldSinglesTop50: 100, OldDoublesTop50: 100,
            NewTop50: 100, NewSinglesTop50: 100, NewDoublesTop50: 100,
            OldCompetitive: 20.0, NewCompetitive: 20.0,
            OldSinglesCompetitive: 20.0, NewSinglesCompetitive: 20.0,
            OldDoublesCompetitive: 20.0, NewDoublesCompetitive: 20.0,
            CoOpRating: 0, PassCount: 0);

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
