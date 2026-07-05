using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Ucs.Contracts.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class CommunitySagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateCommunityThrowsWhenCommunityNameAlreadyExists()
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
    public async Task CreateCommunitySavesNewCommunityWithCurrentUserAsOwnerAndMember()
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
    public async Task JoinCommunityIsIdempotentForExistingMembers()
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
    public async Task JoinCommunityAddsMemberToPublicCommunity()
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
    public async Task JoinCommunityThrowsWhenPrivateAndNoInviteCodeProvided()
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
    public async Task LeaveCommunityRemovesMemberFromTheSet()
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
    public async Task CreateInviteLinkThrowsWhenCallerIsNotAMember()
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
    public async Task CreateInviteLinkPersistsTheCodeAndReturnsIt()
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
    public async Task GetMyCommunitiesReturnsPersonalListWhenLoggedInElsePublicList(bool loggedIn)
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
    public async Task GetCommunityHidesPrivateCommunityFromNonMembers()
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
    public async Task NewTitlesAcquiredBroadcastsTitleListToCommunityChannels()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);

        await ctx.Saga.Consume(BuildContext(new NewTitlesAcquiredEvent(userId,
            NewTitles: new[] { "First Title", "Second Title" },
            ParagonUpgrades: new Dictionary<string, string>(),
            Mix: MixEnum.Phoenix)));

        ctx.Bot.Verify(b => b.SendMessages(
            It.Is<IEnumerable<string>>(msgs => msgs.Any(m => m.Contains("alice") && m.Contains("First Title"))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Phoenix2TitleBroadcastCarriesTheMixPrefix()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);

        await ctx.Saga.Consume(BuildContext(new NewTitlesAcquiredEvent(userId,
            NewTitles: new[] { "First Title" },
            ParagonUpgrades: new Dictionary<string, string>(),
            Mix: MixEnum.Phoenix2)));

        ctx.Bot.Verify(b => b.SendMessages(
            It.Is<IEnumerable<string>>(msgs => msgs.All(m => m.StartsWith("[Phoenix 2] "))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PlayerRatingsImprovedDoesNothingWhenUserNotFound()
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
    public async Task PlayerRatingsImprovedSkipsBroadcastWhenNothingActuallyImproved()
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
    public async Task Phoenix2ScoreAnnouncementIsPrefixedWithTheMixName()
    {
        // Locked decision (plan doc): Discord posts get a "[Phoenix 2]" prefix while
        // both mixes run in parallel. Lookups must follow the event's mix so the
        // announcement reads the Phoenix 2 ledger slice.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20)
            .WithMix(MixEnum.Phoenix2).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix2, userId, chart, score: 950000);

        await ctx.Saga.Consume(BuildContext(PlayerScoresUpdatedEvent.Create(Now, userId, MixEnum.Phoenix2,
            new[]
            {
                new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, IsNewPass: true, OldScore: null,
                    NewScore: 950000, Plate: "SuperbGame", IsBroken: false)
            })));

        ctx.Bot.Verify(b => b.SendMessages(
            It.Is<IEnumerable<string>>(msgs => msgs.Any(m => m.Contains("alice"))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        ctx.Bot.Verify(b => b.SendMessages(
            It.Is<IEnumerable<string>>(msgs => msgs.Any(m => !m.StartsWith("[Phoenix 2] "))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PhoenixScoreAnnouncementStaysUnprefixed()
    {
        // Phoenix is today's default context — its posts must NOT gain a prefix.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, chart, score: 950000);

        await ctx.Saga.Consume(BuildContext(PlayerScoresUpdatedEvent.Create(Now, userId, MixEnum.Phoenix,
            new[]
            {
                new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, IsNewPass: true, OldScore: null,
                    NewScore: 950000, Plate: "SuperbGame", IsBroken: false)
            })));

        ctx.Bot.Verify(b => b.SendMessages(
            It.Is<IEnumerable<string>>(msgs => msgs.Any(m => m.Contains("alice"))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        ctx.Bot.Verify(b => b.SendMessages(
            It.Is<IEnumerable<string>>(msgs => msgs.Any(m => m.Contains("[Phoenix"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LevelProgressStatsChunkAcrossMessagesInsteadOfOverflowingOneSend()
    {
        // 12 distinct (type, level) groups — unchunked, the progress summary grows past
        // Discord's 2,000-char content cap on import-sized events and the send is
        // silently dropped by the per-message catch.
        var userId = Guid.NewGuid();
        var charts = Enumerable.Range(10, 12)
            .Select(level => new ChartBuilder().WithType(ChartType.Single).WithLevel(level).Build())
            .ToArray();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, charts, score: 950000);

        await ctx.Saga.Consume(BuildContext(PlayerScoresUpdatedEvent.Create(Now, userId, MixEnum.Phoenix,
            charts.Select(c => new PlayerScoresUpdatedEvent.ScoreChange(c.Id, IsNewPass: true,
                OldScore: null, NewScore: 950000, Plate: "SuperbGame", IsBroken: false)).ToArray())));

        // Pass list + two stats chunks (10 groups, then the remaining 2).
        ctx.Bot.Verify(b => b.SendMessages(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
        // The tail groups past the first chunk still arrive.
        ctx.Bot.Verify(b => b.SendMessages(
            It.Is<IEnumerable<string>>(msgs => msgs.Any(m => m.Contains("#DIFFICULTY|S10# 1/1"))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UcsPlacementBroadcastsFromEventFactsAlone()
    {
        // The fat event carries everything the Discord post needs — the saga must not
        // reach back into UCS storage (it no longer can: IUcsRepository is UCS-internal).
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);

        await ctx.Saga.Consume(BuildContext(UcsLeaderboardPlacedEvent.Create(
            Now, userId, Guid.NewGuid(), score: 950000, plate: "SuperbGame", isBroken: false,
            artist: "StepMaker", songName: "Test Song", difficulty: "S15")));

        ctx.Bot.Verify(b => b.SendMessages(
            It.Is<IEnumerable<string>>(msgs => msgs.Any(m => m.Contains("alice")
                                                             && m.Contains("950000")
                                                             && m.Contains("StepMaker")
                                                             && m.Contains("Test Song")
                                                             && m.Contains("S15"))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UserUpdatedJoinsWorldAndCountryWhenPublic()
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
    public async Task UserUpdatedLeavesWorldWhenNoLongerPublic()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenCommunityExists("World");

        await ctx.Saga.Consume(BuildContext(new UserUpdatedEvent(userId, Country: null, IsPublic: false)));

        ctx.Mediator.Verify(m => m.Send(
            It.Is<LeaveCommunityCommand>(l => (string)l.CommunityName == "World" && l.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(It.IsAny<JoinCommunityCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UserUpdatedSkipsLeaveWhenWorldDoesNotExist()
    {
        // A fresh database has no World community — leaving it must be a no-op, not a throw.
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();

        await ctx.Saga.Consume(BuildContext(new UserUpdatedEvent(userId, Country: null, IsPublic: false)));

        ctx.Mediator.Verify(m => m.Send(It.IsAny<LeaveCommunityCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UserUpdatedCreatesWorldCommunityOnFirstJoin()
    {
        // Nothing seeds system communities — they create themselves: public, regional, unowned.
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();

        await ctx.Saga.Consume(BuildContext(new UserUpdatedEvent(userId, Country: null, IsPublic: true)));

        ctx.Communities.Verify(c => c.SaveCommunity(
            It.Is<Community>(comm => (string)comm.Name == "World" && comm.IsRegional
                                     && comm.PrivacyType == CommunityPrivacyType.Public
                                     && comm.OwnerId == Guid.Empty),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(
            It.Is<JoinCommunityCommand>(j => (string)j.CommunityName == "World" && j.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UserUpdatedCreatesCountryCommunityOnFirstJoin()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenCommunityExists("World");

        await ctx.Saga.Consume(BuildContext(new UserUpdatedEvent(userId, Country: "Peru", IsPublic: true)));

        ctx.Communities.Verify(c => c.SaveCommunity(
            It.Is<Community>(comm => (string)comm.Name == "Peru" && comm.IsRegional),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(
            It.Is<JoinCommunityCommand>(j => (string)j.CommunityName == "Peru" && j.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UserUpdatedDoesNotRecreateExistingSystemCommunities()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenCommunityExists("World");
        ctx.GivenCommunityExists("Peru");

        await ctx.Saga.Consume(BuildContext(new UserUpdatedEvent(userId, Country: "Peru", IsPublic: true)));

        ctx.Communities.Verify(c => c.SaveCommunity(It.IsAny<Community>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class HandlerContext
    {
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<ICommunityRepository> Communities { get; } = new();
        public Mock<IBotClient> Bot { get; } = new();
        public Mock<IUserReader> Users { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
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
                Charts.Object, Scores.Object, Mediator.Object, DateTime.Object);
        }

        public void GivenCommunityExists(string name)
        {
            Communities.Setup(c => c.GetCommunityByName(It.Is<Name>(n => (string)n == name),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Community(Name.From(name), Guid.Empty, CommunityPrivacyType.Public, true));
        }

        public void GivenUser(Guid userId, string name)
        {
            Users.Setup(u => u.GetUser(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserBuilder().WithId(userId).WithName(name).Build());
        }

        public void GivenScoreAnnouncementLookups(MixEnum mix, Guid userId, Chart chart, int score)
        {
            Scores.Setup(s => s.GetBestScores(mix, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new RecordedPhoenixScore(chart.Id, score, PhoenixPlate.SuperbGame, false, Now)
                });
            Scores.Setup(s => s.GetClearCount(mix, userId, chart.Type, chart.Level,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            Charts.Setup(c => c.GetCharts(mix, It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                    It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { chart });
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { chart });
        }

        public void GivenScoreAnnouncementLookups(MixEnum mix, Guid userId, Chart[] charts, int score)
        {
            Scores.Setup(s => s.GetBestScores(mix, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts.Select(c =>
                    new RecordedPhoenixScore(c.Id, score, PhoenixPlate.SuperbGame, false, Now)).ToArray());
            Scores.Setup(s => s.GetClearCount(mix, userId, It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            Charts.Setup(c => c.GetCharts(mix, It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                    It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
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
            CoOpRating: 0, PassCount: 0, Mix: MixEnum.Phoenix);

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
