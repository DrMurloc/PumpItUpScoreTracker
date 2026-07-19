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
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
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
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
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
    public async Task JoinCommunityRejectsABannedUser()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext(currentUserId: userId);
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.Public,
            new[] { new CommunityMember(userId, CommunityRole.Banned, CommunityPermission.None, null, null) },
            Array.Empty<Community.ChannelConfiguration>(), new Dictionary<Guid, DateOnly?>(), false,
            Community.DefaultAdminPermissionsSeed, null);
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        await Assert.ThrowsAsync<DeniedFromCommunityException>(() =>
            ctx.Saga.Handle(new JoinCommunityCommand(Name.From("Acme"), null), CancellationToken.None));
    }

    [Fact]
    public async Task InvitePreviewReturnsNullForAnUnknownCode()
    {
        var ctx = new HandlerContext();
        ctx.Communities.Setup(c => c.GetCommunityByInviteCode(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Name?)null);

        var preview = await ctx.Saga.Handle(new GetCommunityInvitePreviewQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(preview);
    }

    [Fact]
    public async Task InvitePreviewReturnsCommunityShapeAndCallerStanding()
    {
        var userId = Guid.NewGuid();
        var code = Guid.NewGuid();
        var ctx = new HandlerContext(currentUserId: userId);
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.PublicWithCode,
            new[] { Guid.NewGuid(), Guid.NewGuid() }, Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?> { [code] = null }, false);
        ctx.Communities.Setup(c => c.GetCommunityByInviteCode(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Name.From("Acme"));
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        var preview = await ctx.Saga.Handle(new GetCommunityInvitePreviewQuery(code), CancellationToken.None);

        Assert.NotNull(preview);
        Assert.Equal("Acme", (string)preview!.CommunityName);
        Assert.Equal(2, preview.MemberCount);
        Assert.False(preview.IsExpired);
        Assert.False(preview.IsBanned);
        Assert.False(preview.IsAlreadyMember);
    }

    [Fact]
    public async Task InvitePreviewFlagsAnExpiredCodeAndABannedCaller()
    {
        var userId = Guid.NewGuid();
        var code = Guid.NewGuid();
        var ctx = new HandlerContext(currentUserId: userId);
        var community = new Community(Name.From("Acme"), Guid.NewGuid(), CommunityPrivacyType.Private,
            new[] { new CommunityMember(userId, CommunityRole.Banned, CommunityPermission.None, null, null) },
            Array.Empty<Community.ChannelConfiguration>(),
            new Dictionary<Guid, DateOnly?> { [code] = new DateOnly(2020, 1, 1) }, false,
            Community.DefaultAdminPermissionsSeed, null);
        ctx.Communities.Setup(c => c.GetCommunityByInviteCode(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Name.From("Acme"));
        ctx.Communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(community);

        var preview = await ctx.Saga.Handle(new GetCommunityInvitePreviewQuery(code), CancellationToken.None);

        Assert.NotNull(preview);
        Assert.True(preview!.IsExpired);
        Assert.True(preview.IsBanned);
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
    public async Task NewTitlesWithNoSessionRenderAsARichTitlesCard()
    {
        // Zero-score imports (and admin recomputes) still surface title completions — now as
        // a rich card in the session card's language, not a plain-text list (owner Q1=a).
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);

        await ctx.Saga.Consume(BuildContext(new NewTitlesAcquiredEvent(userId,
            NewTitles: new[] { "First Title", "Second Title" },
            ParagonUpgrades: new Dictionary<string, string>(),
            Mix: MixEnum.Phoenix)));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Any(m =>
                m.Header!.Markdown.Contains("alice")
                && m.Blocks.OfType<RichBotText>().Any(t =>
                    t.Markdown.Contains("🏅 **[First Title]** completed")
                    && t.Markdown.Contains("🏅 **[Second Title]** completed")))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TitlesCardRendersParagonGainsWithTheirGradeEmoji()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);

        await ctx.Saga.Consume(BuildContext(new NewTitlesAcquiredEvent(userId,
            NewTitles: Array.Empty<string>(),
            ParagonUpgrades: new Dictionary<string, string> { ["Expert Lv. 2"] = "PG" },
            Mix: MixEnum.Phoenix)));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Any(m =>
                m.Blocks.OfType<RichBotText>().Any(t =>
                    t.Markdown.Contains("🏅 **[Expert Lv. 2]** paragon → #PLATE|PerfectGame#")))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Phoenix2TitlesCardCarriesTheMixPrefix()
    {
        var userId = Guid.NewGuid();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);

        await ctx.Saga.Consume(BuildContext(new NewTitlesAcquiredEvent(userId,
            NewTitles: new[] { "First Title" },
            ParagonUpgrades: new Dictionary<string, string>(),
            Mix: MixEnum.Phoenix2)));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.All(m => m.Header!.Markdown.Contains("[Phoenix 2] "))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Phoenix2ScoreCardIsPrefixedWithTheMixName()
    {
        // Locked decision (plan doc): Discord posts get a "[Phoenix 2]" prefix while
        // both mixes run in parallel. Lookups must follow the event's mix so the
        // card reads the Phoenix 2 ledger slice.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20)
            .WithMix(MixEnum.Phoenix2).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix2, userId, chart, score: 950000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix2, null,
            (chart.Id, true, HighlightFlags.None))));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Any(m =>
                m.Header!.Markdown.Contains("[Phoenix 2] ") && m.Header.Markdown.Contains("alice")
                && m.Footer!.Contains("#MIX|Phoenix2#"))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PhoenixScoreCardStaysUnprefixedAndCarriesFlagsAndArt()
    {
        // Phoenix is today's default context — its cards must NOT gain a prefix. The
        // capture flags render as glyphs on the pass rows.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, chart, score: 950000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null,
            (chart.Id, true, HighlightFlags.PumbilityTop50))));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Any(m =>
                !m.Header!.Markdown.Contains("[Phoenix")
                && m.Blocks.OfType<RichBotSection>().Any(s => s.Thumbnail != null && s.Markdown.Contains("👑")))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportSizedEventsCollapseToOneDigestCard()
    {
        // Above the digest threshold, one calm card per event — never a message wall.
        var userId = Guid.NewGuid();
        var charts = Enumerable.Range(0, 30)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build())
            .ToArray();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, charts, score: 950000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, Guid.NewGuid(),
            charts.Select(c => (c.Id, true, HighlightFlags.None)).ToArray())));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Count() == 1
                                                       && msgs.Single().Header!.Markdown.Contains("passed 30")),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeepLinkButtonRendersOnlyForPublicPlayers()
    {
        var publicUser = Guid.NewGuid();
        var privateUser = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.Users.Setup(u => u.GetUser(publicUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(publicUser).WithName("open").WithIsPublic(true).Build());
        ctx.Users.Setup(u => u.GetUser(privateUser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(privateUser).WithName("shy").WithIsPublic(false).Build());
        ctx.GivenUserCommunitiesWithChannel(publicUser, communityName: "Acme", channelId: 12345);
        ctx.GivenUserCommunitiesWithChannel(privateUser, communityName: "Bcme", channelId: 54321);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, publicUser, chart, score: 950000);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, privateUser, chart, score: 950000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(publicUser, MixEnum.Phoenix, sessionId,
            (chart.Id, true, HighlightFlags.None))));
        await ctx.Saga.Consume(BuildContext(CapturedEvent(privateUser, MixEnum.Phoenix, sessionId,
            (chart.Id, true, HighlightFlags.None))));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.All(m =>
                m.Links.Count == 1 && m.Links.Single().Url.ToString().Contains($"/Player/{publicUser}/Sessions")
                && m.Links.Single().Url.ToString().Contains(sessionId.ToString()))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.All(m => !m.Links.Any())),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(54321ul)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnremarkablePassesFillTheMoreScoresBucketThenOverflow()
    {
        // Owner call: non-highlighted scores now show — up to 10 compact one-liners in the
        // "More scores" bucket (no art), the rest compress into "+N more", and the folder
        // progress + header totals still summarize.
        var userId = Guid.NewGuid();
        var charts = Enumerable.Range(10, 12)
            .Select(level => new ChartBuilder().WithType(ChartType.Single).WithLevel(level).Build())
            .ToArray();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, charts, score: 950000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null,
            charts.Select(c => (c.Id, true, HighlightFlags.None)).ToArray())));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Count() == 1
                && msgs.Single().Header!.Markdown.Contains("passed 12")
                && !msgs.Single().Blocks.OfType<RichBotSection>().Any()
                && msgs.Single().Blocks.OfType<RichBotText>().Any(t => t.Markdown.Contains("-# More scores"))
                && msgs.Single().Blocks.OfType<RichBotText>().Any(t => t.Markdown.Contains("+2 more"))
                && msgs.Single().Blocks.OfType<RichBotText>().Any(t => t.Markdown.Contains("#DIFFICULTY|S21# 1/1"))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MoreScoresStayDescendingByLevelWhenNotableOverflows()
    {
        // Bug: when the notable bucket overflows its cap, the extra flagged (low-level) rows
        // used to leak into "More scores" ahead of higher-level unflagged charts — two
        // descending runs, not one. The bucket must be a single level-desc run.
        var userId = Guid.NewGuid();
        var flaggedLow = Enumerable.Range(0, 11)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(5).Build()).ToArray();
        var plainHigh = new ChartBuilder().WithType(ChartType.Single).WithLevel(23).Build();
        var all = flaggedLow.Append(plainHigh).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, all, score: 950000);

        var changes = flaggedLow.Select(c => (c.Id, true, HighlightFlags.ScoreQuality90))
            .Append((plainHigh.Id, true, HighlightFlags.None)).ToArray();
        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null, changes)));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => MoreScoresAreLevelDescending(msgs)),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // The "More scores" bucket, minus its label line, must be sorted by level descending.
    private static bool MoreScoresAreLevelDescending(IEnumerable<RichBotMessage> msgs)
    {
        var rows = msgs.Single().Blocks.OfType<RichBotText>()
            .First(t => t.Markdown.StartsWith("-# More scores")).Markdown
            .Split('\n').Skip(1).ToArray();
        var levels = rows.Select(r =>
        {
            var start = r.IndexOf("#DIFFICULTY|", StringComparison.Ordinal) + "#DIFFICULTY|S".Length;
            var end = r.IndexOf('#', start);
            return int.Parse(r[start..end]);
        }).ToArray();
        return levels.SequenceEqual(levels.OrderByDescending(l => l));
    }

    [Fact]
    public async Task StatsAndAchievementsOpenTheCardAsTheirOwnSections()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, chart, score: 950000);
        var milestones = new[]
        {
            new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, sessionId, Now, 21480, 21530, null, null),
            new PlayerMilestoneRecord(MilestoneKind.SinglesCompetitiveGain, sessionId, Now, 21.416, 21.447,
                null, null),
            new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, sessionId, Now, null, null,
                "Intermediate Lv. 10", null),
            new PlayerMilestoneRecord(MilestoneKind.ParagonLevelGain, sessionId, Now, null, null,
                "Intermediate Lv. 7", "SS")
        };

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, sessionId, milestones,
            (chart.Id, true, HighlightFlags.None))));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Single().Blocks.OfType<RichBotText>().Any(t =>
                    t.Markdown.Contains("📈 **PUMBILITY** 21,480 → **21,530** (+50)")
                    && t.Markdown.Contains("📈 **Singles competitive** 21.42 → **21.45**"))
                && msgs.Single().Blocks.OfType<RichBotText>().Any(t =>
                    t.Markdown.Contains("🏅 **[Intermediate Lv. 10]** completed")
                    && t.Markdown.Contains("🏅 **[Intermediate Lv. 7]** paragon → #LETTERGRADE|SS|False#"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EveryTitleCompletionIsListedWithNoNameCap()
    {
        // Owner call: titles are the card's top priority — list them ALL (the 4000-char
        // budget is the only backstop), never "…and N more titles". Paragons don't aggregate.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, chart, score: 950000);
        var milestones = Enumerable.Range(1, 12)
            .Select(i => new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, null, Now, null, null,
                $"Title {i}", null))
            .Append(new PlayerMilestoneRecord(MilestoneKind.ParagonLevelGain, null, Now, null, null,
                "Expert Lv. 2", "PG"))
            .ToArray();

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null, milestones,
            (chart.Id, true, HighlightFlags.None))));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Single().Blocks.OfType<RichBotText>().Any(t =>
                t.Markdown.Contains("**[Title 1]** completed")
                && t.Markdown.Contains("**[Title 11]** completed")
                && t.Markdown.Contains("**[Title 12]** completed")
                && !t.Markdown.Contains("more titles")
                && t.Markdown.Contains("🏅 **[Expert Lv. 2]** paragon → #PLATE|PerfectGame#"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenericProgressDeltasAlwaysRideTheTopSectionAlongsideCompletions()
    {
        // Owner call (D3): generic title progress shows up top ALWAYS, not only as a
        // nothing-completed fallback — a completion and a delta coexist, both bracketed.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, chart, score: 950000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null,
                new[]
                {
                    new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, null, Now, null, null,
                        "Advanced Lv. 1", null)
                },
                new[] { new TitleProgressDelta("Expert Lv. 4", 0.82, 0.86) },
                (chart.Id, true, HighlightFlags.None))));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Single().Blocks.OfType<RichBotText>().Any(t =>
                t.Markdown.Contains("🏅 **[Advanced Lv. 1]** completed")
                && t.Markdown.Contains("🏅 [Expert Lv. 4] 82% → **86%**"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WeeklyPlacementsFlexOnTheCardCappedAtFour()
    {
        var userId = Guid.NewGuid();
        var charts = Enumerable.Range(16, 6)
            .Select(level => new ChartBuilder().WithType(ChartType.Single).WithLevel(level).Build())
            .ToArray();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, charts, score: 950000);
        ctx.GivenWeeklyPlacements(charts.Select(c => new WeeklyPlacementRecord(c.Id, 2)).ToArray());

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null,
            charts.Select(c => (c.Id, true, HighlightFlags.None)).ToArray())));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Single().Blocks.OfType<RichBotText>().Any(t =>
                t.Markdown.Contains($"🏆 **#2** on {charts[5].Song.Name} #DIFFICULTY|S21# weekly")
                && t.Markdown.Split('\n').Count(line => line.Contains("weekly")) == 4
                && !t.Markdown.Contains("#DIFFICULTY|S17# weekly"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WeeklyPlacementsBelowCompetitiveMinusFiveAreDropped()
    {
        // Owner call: weekly inclusion follows the same competitive − 5 gate as the peer
        // flag. For a 22-competitive player, weekly placements on S16/S12 are noise; the
        // S21/S18 placements stay.
        var userId = Guid.NewGuid();
        var all = new[] { 21, 18, 16, 12 }
            .Select(l => new ChartBuilder().WithType(ChartType.Single).WithLevel(l).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, all, score: 950000);
        ctx.GivenCompetitive(singles: 22);
        ctx.GivenWeeklyPlacements(all.Select(c => new WeeklyPlacementRecord(c.Id, 3)).ToArray());

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null,
            all.Select(c => (c.Id, true, HighlightFlags.None)).ToArray())));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Single().Blocks.OfType<RichBotText>().Any(t =>
                t.Markdown.Contains("#DIFFICULTY|S21# weekly")
                && t.Markdown.Contains("#DIFFICULTY|S18# weekly")
                && !t.Markdown.Contains("#DIFFICULTY|S16# weekly")
                && !t.Markdown.Contains("#DIFFICULTY|S12# weekly"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CoOpsShowAsACompactBucketCappedAtFive()
    {
        var userId = Guid.NewGuid();
        var coOps = Enumerable.Range(0, 7)
            .Select(_ => new ChartBuilder().WithType(ChartType.CoOp).WithLevel(2).Build())
            .ToArray();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, coOps, score: 993000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null,
            coOps.Select(c => (c.Id, false, HighlightFlags.None)).ToArray())));

        // Co-ops drop their art rows (owner call): up to 5 compact one-liners in their own
        // labelled bucket, the remainder compressing with a CO-OP count; header still marks CO-OP.
        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => !msgs.Single().Blocks.OfType<RichBotSection>().Any()
                && msgs.Single().Blocks.OfType<RichBotText>().Any(t => t.Markdown.Contains("-# Co-op")
                    && t.Markdown.Split('\n').Count(l => l.Contains("#DIFFICULTY|")) == 5)
                && msgs.Single().Blocks.OfType<RichBotText>().Any(t => t.Markdown.Contains("+2 more: CO-OP ×2"))
                && msgs.Single().Header!.Markdown.Contains("CO-OP")),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheSessionsBiggestGainEarnsAnArtRowTheOthersFallToMoreScores()
    {
        var userId = Guid.NewGuid();
        var biggest = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).Build();
        var smaller = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, new[] { biggest, smaller }, score: 950000);

        // biggest: +27,704; smaller: +12,000 — over the threshold too, but only the session's
        // single biggest gain earns the 💥 art row (owner call); the other is a compact row.
        await ctx.Saga.Consume(BuildContext(ScoreHighlightsCapturedEvent.Create(Now, userId, MixEnum.Phoenix,
            null,
            new[]
            {
                new ScoreHighlightsCapturedEvent.HighlightedChange(biggest.Id, false, 922296, 950000,
                    "TalentedGame", false, HighlightFlags.None),
                new ScoreHighlightsCapturedEvent.HighlightedChange(smaller.Id, false, 938000, 950000,
                    "TalentedGame", false, HighlightFlags.None)
            })));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs =>
                msgs.Single().Blocks.OfType<RichBotSection>().Single().Markdown
                    .Contains("💥 Biggest gain of the session")
                && msgs.Single().Blocks.OfType<RichBotText>().Any(t => t.Markdown.Contains("-# More scores")
                    && t.Markdown.Contains("#DIFFICULTY|S22#"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnrichedCaptionsRenderRankPeersOrdinalAndSkillScore()
    {
        // #5–#8: the per-row caption renders the captured detail — pumbility rank, peer
        // standing, skill title score/threshold, folder-debut ordinal — not generic text.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(24).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, chart, score: 972000);
        var detail = new HighlightDetail(PumbilityRank: 4, FolderDebutOrdinal: 1, PeerCount: 47,
            PeerBetterCount: 2, PeerPgCount: 0, SkillTitleName: "[DRILL] Lv.10", SkillTitleScore: 972000,
            SkillTitleThreshold: 990000);
        var change = new ScoreHighlightsCapturedEvent.HighlightedChange(chart.Id, true, null, 972000,
            "SuperbGame", false,
            HighlightFlags.PumbilityTop50 | HighlightFlags.ScoreQuality90 | HighlightFlags.TitleProgress
            | HighlightFlags.FolderDebut, detail);

        await ctx.Saga.Consume(BuildContext(ScoreHighlightsCapturedEvent.Create(Now, userId, MixEnum.Phoenix,
            null, new[] { change })));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Single().Blocks.OfType<RichBotSection>().Any(s =>
                s.Markdown.Contains("👑 #4 in your PUMBILITY")
                && s.Markdown.Contains("📊 #3 of 47 peers")
                && s.Markdown.Contains("🏅 [DRILL] Lv.10 (972k/990k)")
                && s.Markdown.Contains("🆕 First D24"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheAccentStripeIsTheMixBrandColor()
    {
        // Owner call (2026-07-05, reversing the earlier grade-accent decision): with
        // two mixes running in parallel, the stripe identifies the MIX at a glance —
        // grades already color every row via their emojis.
        var userId = Guid.NewGuid();
        var phoenixChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var phoenix2Chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20)
            .WithMix(MixEnum.Phoenix2).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, phoenixChart, score: 950000);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix2, userId, phoenix2Chart, score: 950000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null,
            (phoenixChart.Id, true, HighlightFlags.None))));
        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix2, null,
            (phoenix2Chart.Id, true, HighlightFlags.None))));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs =>
                msgs.Single().AccentColor == MixEnum.Phoenix.GetAccentColor()),
            It.IsAny<IEnumerable<ulong>>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs =>
                msgs.Single().AccentColor == MixEnum.Phoenix2.GetAccentColor()),
            It.IsAny<IEnumerable<ulong>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MilestoneBannerLeadsTheCardAsItsOwnBand()
    {
        // Folder lamps arrive ON the captured event and render as a banner band between
        // separators — the loudest thing on the card short of the header.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(20).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, chart, score: 950000);
        var lamps = new[]
        {
            new PlayerMilestoneRecord(MilestoneKind.FolderPassLamp, null, Now, null, null, null, "D20"),
            new PlayerMilestoneRecord(MilestoneKind.FolderPlateLamp, null, Now, null, null, null,
                "D20|FairGame")
        };

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null, lamps,
            (chart.Id, true, HighlightFlags.None))));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Any(m =>
                m.Blocks.OfType<RichBotText>().Any(t =>
                    t.Markdown.Contains("#DIFFICULTY|D20# **All passed!**")
                    && t.Markdown.Contains("**All #PLATE|FairGame# or better**")))),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FlaggedRowsLeadTheCardAndSpellOutTheirFlags()
    {
        // A flagged pass outranks a higher-level unflagged one for the art slots, its
        // song name is bold, and the flag renders as a named subtext caption — not a
        // bare glyph squeezed into the title line.
        var userId = Guid.NewGuid();
        var flaggedChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var plainChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, new[] { flaggedChart, plainChart },
            score: 950000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null,
            (flaggedChart.Id, true, HighlightFlags.ScoreQuality90 | HighlightFlags.FolderDebut),
            (plainChart.Id, true, HighlightFlags.None))));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Any(m =>
                m.Blocks.OfType<RichBotSection>().First(s => s.Thumbnail != null).Markdown
                    .Contains($"**[{flaggedChart.Song.Name}]")
                && m.Blocks.OfType<RichBotSection>().First(s => s.Thumbnail != null).Markdown
                    .Contains("-# 📊 Top scores among peers · 🆕 Folder debut")
                && m.Blocks.OfType<RichBotDivider>().Count() > 1)),
            It.Is<IEnumerable<ulong>>(ids => ids.Contains(12345ul)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DigestCardCarriesTheMilestoneBannerToo()
    {
        var userId = Guid.NewGuid();
        var charts = Enumerable.Range(0, 30)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build())
            .ToArray();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, charts, score: 950000);
        var lamps = new[]
            { new PlayerMilestoneRecord(MilestoneKind.FolderPassLamp, null, Now, null, null, null, "S18") };

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, Guid.NewGuid(), lamps,
            charts.Select(c => (c.Id, true, HighlightFlags.None)).ToArray())));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs => msgs.Count() == 1
                && msgs.Single().Blocks.OfType<RichBotText>().Any(t =>
                    t.Markdown.Contains("#DIFFICULTY|S18# **All passed!**"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheFolderBreakdownSurvivesAScoreHeavyCard()
    {
        // Owner call: the folder line is reserved before scores fill in, so a card packed
        // with notable rows still ends with it — the scores overflow to the count line.
        var userId = Guid.NewGuid();
        var charts = Enumerable.Range(0, 40)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build()).ToArray();
        var ctx = new HandlerContext();
        ctx.GivenUser(userId, name: "alice");
        ctx.GivenUserCommunitiesWithChannel(userId, communityName: "Acme", channelId: 12345);
        ctx.GivenScoreAnnouncementLookups(MixEnum.Phoenix, userId, charts, score: 950000);

        await ctx.Saga.Consume(BuildContext(CapturedEvent(userId, MixEnum.Phoenix, null,
            charts.Select(c => (c.Id, true, HighlightFlags.ScoreQuality90)).ToArray())));

        ctx.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(msgs =>
                msgs.Single().Blocks.OfType<RichBotText>().Any(t => t.Markdown.Contains("#DIFFICULTY|S20# 1/40"))
                && msgs.Single().Blocks.OfType<RichBotText>().Any(t => t.Markdown.Contains(" more:"))),
            It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ScoreHighlightsCapturedEvent CapturedEvent(Guid userId, MixEnum mix, Guid? sessionId,
        params (Guid ChartId, bool IsNewPass, HighlightFlags Flags)[] changes)
    {
        return CapturedEvent(userId, mix, sessionId, Array.Empty<PlayerMilestoneRecord>(), changes);
    }

    private static ScoreHighlightsCapturedEvent CapturedEvent(Guid userId, MixEnum mix, Guid? sessionId,
        PlayerMilestoneRecord[] milestones,
        params (Guid ChartId, bool IsNewPass, HighlightFlags Flags)[] changes)
    {
        return CapturedEvent(userId, mix, sessionId, milestones, Array.Empty<TitleProgressDelta>(), changes);
    }

    private static ScoreHighlightsCapturedEvent CapturedEvent(Guid userId, MixEnum mix, Guid? sessionId,
        PlayerMilestoneRecord[] milestones, TitleProgressDelta[] titleProgress,
        params (Guid ChartId, bool IsNewPass, HighlightFlags Flags)[] changes)
    {
        return ScoreHighlightsCapturedEvent.Create(Now, userId, mix, sessionId,
            changes.Select(c => new ScoreHighlightsCapturedEvent.HighlightedChange(c.ChartId, c.IsNewPass,
                c.IsNewPass ? null : 900000, 950000, "SuperbGame", false, c.Flags)).ToArray(), milestones,
            titleProgress);
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

    [Fact]
    public async Task CommunityCountReturnsTheNonRegionalCount()
    {
        var ctx = new HandlerContext();
        ctx.Communities.Setup(c => c.CountNonRegionalCommunities(It.IsAny<CancellationToken>()))
            .ReturnsAsync(58);

        var result = await ctx.Saga.Handle(new GetCommunityCountQuery(), CancellationToken.None);

        Assert.Equal(58, result);
    }

    private sealed class HandlerContext
    {
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<ICommunityRepository> Communities { get; } = new();
        public Mock<IBotClient> Bot { get; } = new();
        public Mock<IUserReader> Users { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IPlayerStatsReader> PlayerStats { get; } = new();
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
            // Default competitive levels of 0 leave the weekly gate wide open (threshold −5);
            // tests exercising the gate raise them with GivenCompetitive.
            PlayerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Stats(0, 0));
            Saga = new CommunitySaga(CurrentUser.Object, Communities.Object, Bot.Object, Users.Object,
                Charts.Object, Scores.Object, Mediator.Object, PlayerStats.Object, DateTime.Object);
        }

        private static PlayerStatsRecord Stats(double singlesCompetitive, double doublesCompetitive)
        {
            return new PlayerStatsRecord(Guid.Empty, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1,
                0, singlesCompetitive, doublesCompetitive);
        }

        public void GivenCompetitive(double singles, double doubles = 0)
        {
            PlayerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(Stats(singles, doubles));
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
            Mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, double>());
            GivenSnapshotSideReads();
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
            Mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, double>());
            GivenSnapshotSideReads();
        }

        // The snapshot's render-time side reads: weekly placements and co-op difficulty
        // ratings both default to empty.
        private void GivenSnapshotSideReads()
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetUserWeeklyPlacementsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<WeeklyPlacementRecord>());
            Mediator.Setup(m => m.Send(It.IsAny<GetCoOpRatingsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<CoOpRating>());
        }

        public void GivenWeeklyPlacements(params WeeklyPlacementRecord[] placements)
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetUserWeeklyPlacementsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(placements);
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

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
