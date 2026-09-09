using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The four facts, one at a time. Every test starts from a member who qualifies on all of them
///     and knocks exactly one down, because the rule is an AND and a bug in it looks like one
///     conjunct quietly not being read.
/// </summary>
public sealed class DiscordRoleSagaTests
{
    private const ulong Guild = 900;
    private const ulong Snowflake = 42;
    private const ulong BronzeRole = 1;
    private const ulong GoldRole = 2;
    private const ulong BreakerRole = 3;
    private const ulong AboveBotRole = 9;

    private static readonly Guid CommunityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IBotClient> _bot = new();
    private readonly Mock<ICommunityRepository> _communities = new();
    private readonly Mock<IDiscordRoleRepository> _roles = new();
    private readonly Mock<ITitleRepository> _titles = new();
    private readonly Mock<IUserReader> _users = new();
    private readonly Mock<IMediator> _mediator = new();

    public DiscordRoleSagaTests()
    {
        _roles.Setup(r => r.GetServer(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunityDiscordServerRecord(CommunityId, Guild, "Arrow Eclipse", Now));
        _roles.Setup(r => r.GetMappings(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CommunityTitleRoleRecord("[P.B] BRONZE", BronzeRole),
                new CommunityTitleRoleRecord("[P.B] GOLD", GoldRole),
                new CommunityTitleRoleRecord("[PHOENIX] SINGLE BOSS BREAKER", BreakerRole)
            });
        _roles.Setup(r => r.GetGrants(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityDiscordGrantRecord>());
        _roles.Setup(r => r.GetGrantsForUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityDiscordGrantRecord>());

        _bot.Setup(b => b.GetGuildRoles(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BotGuildRole(BronzeRole, "@Bronze", null, null),
                new BotGuildRole(GoldRole, "@Gold", null, null),
                new BotGuildRole(BreakerRole, "@Boss Breaker", null, null),
                new BotGuildRole(AboveBotRole, "@Moderator", null, BotRoleBlockedReason.AboveBot)
            });
        _bot.Setup(b => b.GetMemberRoles(Guild, Snowflake, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ulong>());
        AlreadyHolds();

        InCommunity(CommunityRole.Member);
        HasDiscordLinked();
        Holds("[P.B] BRONZE");
    }

    // ---- setup helpers ----------------------------------------------------------------------

    private void InCommunity(CommunityRole? role)
    {
        _communities.Setup(c => c.GetMemberRoles(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role == null
                ? Array.Empty<CommunityMemberRoleRecord>()
                : new[] { new CommunityMemberRoleRecord(UserId, role.Value) });
    }

    private void HasDiscordLinked(bool linked = true)
    {
        _users.Setup(u => u.GetExternalLogins(It.IsAny<IEnumerable<Guid>>(), "Discord",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(linked
                ? new Dictionary<Guid, string> { [UserId] = Snowflake.ToString() }
                : new Dictionary<Guid, string>());
    }

    /// <summary>
    ///     Titles come from ONE read scoped to the community's mapped titles, not a per-member
    ///     read of everything somebody holds — so the stub answers the bulk shape.
    /// </summary>
    private void Holds(params string[] titles)
    {
        _titles.Setup(t => t.GetUsersWithTitles(MixEnum.Phoenix2, It.IsAny<IEnumerable<Name>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(titles.Select(t =>
                new TitleAchievedRecord(UserId, Name.From(t), ParagonLevel.None)));
    }

    /// <summary>
    ///     Both reads, because a one-member reconcile fetches by id and a bulk pass reads the
    ///     server's roster once. A test that set only one would pass on the path it happened to
    ///     take and say nothing about the other.
    /// </summary>
    private void AlreadyHolds(params ulong[] roleIds)
    {
        _bot.Setup(b => b.GetMemberRoles(Guild, Snowflake, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleIds);
        _bot.Setup(b => b.GetGuildMemberRoles(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ulong, IReadOnlyCollection<ulong>> { [Snowflake] = roleIds });
    }

    private void NotInTheServer()
    {
        _bot.Setup(b => b.GetMemberRoles(Guild, Snowflake, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<ulong>?)null);
        _bot.Setup(b => b.GetGuildMemberRoles(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ulong, IReadOnlyCollection<ulong>>());
    }

    private DiscordRoleSaga Saga() => new(_roles.Object, _communities.Object, _titles.Object,
        _users.Object, _bot.Object, _mediator.Object, FakeDateTime.At(Now).Object,
        NullLogger<DiscordRoleSaga>.Instance);

    private void VerifyGranted(ulong roleId, Times times) =>
        _bot.Verify(b => b.AddRole(Guild, Snowflake, roleId, It.IsAny<CancellationToken>()), times);

    private void VerifyRevoked(ulong roleId, Times times) =>
        _bot.Verify(b => b.RemoveRole(Guild, Snowflake, roleId, It.IsAny<CancellationToken>()), times);

    private void VerifyNothingWritten()
    {
        _bot.Verify(b => b.AddRole(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<ulong>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _bot.Verify(b => b.RemoveRole(It.IsAny<ulong>(), It.IsAny<ulong>(), It.IsAny<ulong>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- all four true ----------------------------------------------------------------------

    [Fact]
    public async Task AMemberWhoQualifiesGetsTheRoleAndIsRemembered()
    {
        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyGranted(BronzeRole, Times.Once());
        _roles.Verify(r => r.SaveGrant(CommunityId, UserId, Snowflake, Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---- each fact knocked down -------------------------------------------------------------

    [Fact]
    public async Task LeavingTheCommunityTakesTheRoleBack()
    {
        InCommunity(null);
        AlreadyHolds(BronzeRole);

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyRevoked(BronzeRole, Times.Once());
        _roles.Verify(r => r.DeleteGrant(CommunityId, UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABannedMemberIsNotAMember()
    {
        InCommunity(CommunityRole.Banned);
        AlreadyHolds(BronzeRole);

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyRevoked(BronzeRole, Times.Once());
    }

    [Fact]
    public async Task NotBeingInTheServerWritesNothingAtAll()
    {
        NotInTheServer();

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyNothingWritten();
    }

    [Fact]
    public async Task NoLinkedDiscordAndNoPriorGrantIsANoOp()
    {
        HasDiscordLinked(false);

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyNothingWritten();
        _roles.Verify(r => r.SaveGrant(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ulong>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     Unlinking Discord leaves no login to read, so the grant row is the only thing that still
    ///     knows which account to take the role off. Without it the role stands forever.
    /// </summary>
    [Fact]
    public async Task UnlinkingDiscordStripsTheRoleUsingTheRememberedAccount()
    {
        HasDiscordLinked(false);
        AlreadyHolds(BronzeRole);
        _roles.Setup(r => r.GetGrantsForUser(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityDiscordGrantRecord(CommunityId, UserId, Snowflake, Now) });

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyRevoked(BronzeRole, Times.Once());
        _roles.Verify(r => r.DeleteGrant(CommunityId, UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LosingNothingButTheTitleStillTakesTheRole()
    {
        Holds();
        AlreadyHolds(BronzeRole);

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyRevoked(BronzeRole, Times.Once());
    }

    // ---- exclusivity ------------------------------------------------------------------------

    [Fact]
    public async Task ClimbingAPumbilityLadderSwapsTheRoleRatherThanStacking()
    {
        Holds("[P.B] BRONZE", "[P.B] GOLD");
        AlreadyHolds(BronzeRole);

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyGranted(GoldRole, Times.Once());
        VerifyRevoked(BronzeRole, Times.Once());
    }

    /// <summary>Only pumbility competes — a boss breaker sits alongside a gem, not instead of it.</summary>
    [Fact]
    public async Task TitlesOffALadderStackWithTheGem()
    {
        Holds("[P.B] GOLD", "[PHOENIX] SINGLE BOSS BREAKER");

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyGranted(GoldRole, Times.Once());
        VerifyGranted(BreakerRole, Times.Once());
        VerifyRevoked(BronzeRole, Times.Never());
    }

    // ---- the silent-failure guards ----------------------------------------------------------

    /// <summary>
    ///     Discord rejects a role above the bot without telling the player, so the write is never
    ///     spent: it would turn a configuration problem the page can show into a silent one.
    /// </summary>
    [Fact]
    public async Task ARoleTheBotCannotAssignIsSkippedRatherThanAttempted()
    {
        _roles.Setup(r => r.GetMappings(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityTitleRoleRecord("[P.B] BRONZE", AboveBotRole) });

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyGranted(AboveBotRole, Times.Never());
    }

    [Fact]
    public async Task AnUnmanagedRoleTheMemberHoldsIsLeftAlone()
    {
        const ulong somebodyElses = 777;
        AlreadyHolds(BronzeRole, somebodyElses);

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyRevoked(somebodyElses, Times.Never());
    }

    [Fact]
    public async Task AMemberAlreadyCorrectCostsNoWrites()
    {
        AlreadyHolds(BronzeRole);

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyNothingWritten();
    }

    /// <summary>A mapping naming a title the game retired stops handing out its role, quietly.</summary>
    [Fact]
    public async Task AMappingForAnUnknownTitleIsIgnored()
    {
        _roles.Setup(r => r.GetMappings(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityTitleRoleRecord("A TITLE THAT NEVER SHIPPED", BronzeRole) });
        Holds("A TITLE THAT NEVER SHIPPED");

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyGranted(BronzeRole, Times.Never());
    }

    [Fact]
    public async Task ACommunityWithNoServerDoesNothing()
    {
        _roles.Setup(r => r.GetServer(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommunityDiscordServerRecord?)null);

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyNothingWritten();
        _bot.Verify(b => b.GetGuildRoles(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ACommunityWithNoMappingsDoesNothing()
    {
        _roles.Setup(r => r.GetMappings(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityTitleRoleRecord>());

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyNothingWritten();
    }

    /// <summary>
    ///     A guild the bot was removed from reports no roles. Nothing is managed, so a reconcile is
    ///     a no-op rather than a mass revocation nobody asked for.
    /// </summary>
    [Fact]
    public async Task AGuildTheBotCannotSeeStripsNobody()
    {
        _bot.Setup(b => b.GetGuildRoles(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BotGuildRole>());
        AlreadyHolds(BronzeRole);

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyNothingWritten();
    }

    /// <summary>A mapping pointing at a deleted role must not be asked for — it no longer exists.</summary>
    [Fact]
    public async Task AMappingPointingAtADeletedRoleIsSkipped()
    {
        _bot.Setup(b => b.GetGuildRoles(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new BotGuildRole(GoldRole, "@Gold", null, null) });

        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        VerifyGranted(BronzeRole, Times.Never());
        VerifyRevoked(BronzeRole, Times.Never());
    }

    // ---- bulk paths -------------------------------------------------------------------------

    /// <summary>
    ///     A sweep over the roster alone never visits somebody who left, so their roles would stand
    ///     forever. The grants are the other half of the work list.
    /// </summary>
    [Fact]
    public async Task TheSweepVisitsPeopleWhoAreNoLongerMembers()
    {
        InCommunity(null);
        AlreadyHolds(BronzeRole);
        _roles.Setup(r => r.GetGrants(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityDiscordGrantRecord(CommunityId, UserId, Snowflake, Now) });

        await Saga().ReconcileCommunity(CommunityId, CancellationToken.None);

        VerifyRevoked(BronzeRole, Times.Once());
    }

    /// <summary>
    ///     The cost regression. Reading a member's titles one at a time pulled every row they hold
    ///     — all 272 titles' worth, each with a ParagonLevel to parse — once per member, and
    ///     fetching each member from Discord separately cost an HTTP round trip each. A sweep over
    ///     a real community took twenty seconds. Both are one read now, and this fails if either
    ///     goes back to being per-member.
    /// </summary>
    [Fact]
    public async Task ASweepReadsTitlesAndTheServerRosterOnceEach()
    {
        InCommunity(CommunityRole.Member);
        _roles.Setup(r => r.GetGrants(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityDiscordGrantRecord(CommunityId, UserId, Snowflake, Now) });

        await Saga().ReconcileCommunity(CommunityId, CancellationToken.None);

        _titles.Verify(t => t.GetCompletedTitles(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _titles.Verify(t => t.GetUsersWithTitles(MixEnum.Phoenix2, It.IsAny<IEnumerable<Name>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _bot.Verify(b => b.GetGuildMemberRoles(Guild, It.IsAny<CancellationToken>()), Times.Once);
        _bot.Verify(b => b.GetMemberRoles(It.IsAny<ulong>(), It.IsAny<ulong>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     Settling ONE person still goes by id — that read needs no privileged intent, and
    ///     downloading a whole server to answer a single title change would be worse than the
    ///     problem it solves.
    /// </summary>
    [Fact]
    public async Task SettlingOneMemberDoesNotDownloadTheWholeServer()
    {
        await Saga().ReconcileOne(CommunityId, UserId, CancellationToken.None);

        _bot.Verify(b => b.GetMemberRoles(Guild, Snowflake, It.IsAny<CancellationToken>()), Times.Once);
        _bot.Verify(b => b.GetGuildMemberRoles(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     The bar is deterministic because the work list is known before the first write. A
    ///     progress report that only ever grew its own denominator would be a spinner with numbers
    ///     on it.
    /// </summary>
    [Fact]
    public async Task ASweepReportsItsWholeWorkListBeforeItStarts()
    {
        InCommunity(CommunityRole.Member);
        var reports = new List<DiscordRoleProgress>();

        await Saga().ReconcileCommunity(CommunityId, CancellationToken.None,
            new Progress<DiscordRoleProgress>(reports.Add));

        // Progress<T> posts to the sync context, so drain it before asserting.
        await Task.Delay(50);
        Assert.NotEmpty(reports);
        Assert.Equal(0, reports[0].Done);
        Assert.All(reports, r => Assert.Equal(reports[0].Total, r.Total));
        Assert.Equal(reports[0].Total, reports[^1].Done);
    }

    [Fact]
    public async Task RevokingAllTakesEveryManagedRoleAndForgetsTheCommunity()
    {
        AlreadyHolds(BronzeRole, GoldRole);
        _roles.Setup(r => r.GetGrants(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityDiscordGrantRecord(CommunityId, UserId, Snowflake, Now) });

        await Saga().RevokeAll(CommunityId, CancellationToken.None);

        VerifyRevoked(BronzeRole, Times.Once());
        VerifyRevoked(GoldRole, Times.Once());
        _roles.Verify(r => r.DeleteGrantsForCommunity(CommunityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     The purge reads the grant row rather than the external login, because Identity deletes
    ///     the login on its first pass and this consumer would be racing it.
    /// </summary>
    [Fact]
    public async Task PurgingAnAccountStripsItsRolesWithoutReadingItsLogin()
    {
        AlreadyHolds(BronzeRole);
        _roles.Setup(r => r.GetGrantsForUser(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityDiscordGrantRecord(CommunityId, UserId, Snowflake, Now) });

        await Saga().RemoveAllForUser(UserId, CancellationToken.None);

        VerifyRevoked(BronzeRole, Times.Once());
        _roles.Verify(r => r.DeleteGrant(CommunityId, UserId, It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SomebodyJoiningTheServerIsSettledInEveryCommunityThatUsesIt()
    {
        _roles.Setup(r => r.GetServersByGuild(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CommunityDiscordServerRecord(CommunityId, Guild, "Arrow Eclipse", Now)
            });
        _mediator.Setup(m => m.Send(
                It.Is<GetUserByExternalLoginQuery>(q =>
                    q.ExternalId == Snowflake.ToString() && q.LoginProviderName == "Discord"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(UserId).Build());

        await Saga().ReconcileGuildMember(Guild, Snowflake, CancellationToken.None);

        VerifyGranted(BronzeRole, Times.Once());
    }

    [Fact]
    public async Task SomebodyJoiningWithNoSiteAccountIsIgnored()
    {
        _roles.Setup(r => r.GetServersByGuild(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CommunityDiscordServerRecord(CommunityId, Guild, "Arrow Eclipse", Now)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(),
            It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await Saga().ReconcileGuildMember(Guild, Snowflake, CancellationToken.None);

        VerifyNothingWritten();
    }
}
