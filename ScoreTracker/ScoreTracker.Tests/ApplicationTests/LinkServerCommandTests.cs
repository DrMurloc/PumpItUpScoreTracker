using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Messages;
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
///     Designating a server hands out standing in somebody's Discord, so it needs BOTH
///     authorities: Manage Server there, and ManageDiscord here. Each test removes one of them.
/// </summary>
public sealed class LinkServerCommandTests
{
    private const ulong Guild = 200;
    private const ulong Invoker = 300;
    private static readonly Guid CommunityId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid UserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IBotClient> _bot = new();
    private readonly Mock<IBus> _bus = new();
    private readonly Mock<ICommunityRepository> _communities = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IDiscordRoleService> _discordRoles = new();
    private readonly Mock<IDiscordFeedSubscriptionRepository> _feeds = new();
    private readonly Mock<ILocalizedTextAccessor> _localizer = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IDiscordRoleRepository> _roleConfiguration = new();

    public LinkServerCommandTests()
    {
        _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>()))
            .Returns((string? _, string key) => key);
        _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string? _, string key, object[] args) => string.Format(key, args));
        _mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(UserId).Build());

        _communities.Setup(c => c.GetCommunityId(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommunityId);
        GivenPermission(CommunityPermission.All);
        _bot.Setup(b => b.GetGuild(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotGuild(Guild, "Arrow Eclipse"));
    }

    private void GivenPermission(CommunityPermission permissions)
    {
        var members = new[]
        {
            new CommunityMember(UserId, CommunityRole.Admin, permissions, null, null)
        };
        _communities.Setup(c => c.GetCommunityByName(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Community(Name.From("Arrow Eclipse"), Guid.NewGuid(),
                CommunityPrivacyType.Public, members, Array.Empty<Community.ChannelConfiguration>(),
                new Dictionary<Guid, DateOnly?>(), false, CommunityPermission.None, null));
    }

    private BotCommandSaga Saga() =>
        new(_bot.Object, _communities.Object, _feeds.Object, _mediator.Object, _currentUser.Object,
            _localizer.Object, _roleConfiguration.Object, _discordRoles.Object,
            FakeDateTime.At(Now).Object, _bus.Object);

    private static HandleBotInteractionCommand Invoke(bool canManageGuild = true, ulong? guildId = Guild,
        string community = "Arrow Eclipse") =>
        new(new BotInteraction(new[] { "link-server" },
            new Dictionary<string, string> { ["community"] = community },
            ChannelId: 100, GuildId: guildId, UserId: Invoker, UserDisplayName: "Tester",
            InvokerCanManageChannels: true, UserLocale: null, InvokerCanManageGuild: canManageGuild));

    [Fact]
    public async Task LinkingRecordsTheServerAndSettlesEverybody()
    {
        await Saga().Handle(Invoke(), CancellationToken.None);

        _roleConfiguration.Verify(r => r.SaveServer(CommunityId, Guild, "Arrow Eclipse", Now,
            It.IsAny<CancellationToken>()), Times.Once);
        _bus.Verify(b => b.Publish(
            It.Is<ReconcileDiscordRolesCommand>(m => m.CommunityId == CommunityId && m.UserId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithoutManageServerNothingIsRecorded()
    {
        await Saga().Handle(Invoke(canManageGuild: false), CancellationToken.None);

        _roleConfiguration.Verify(r => r.SaveServer(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WithoutTheSitePermissionNothingIsRecorded()
    {
        GivenPermission(CommunityPermission.ManageUsers);

        await Saga().Handle(Invoke(), CancellationToken.None);

        _roleConfiguration.Verify(r => r.SaveServer(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunningItOutsideAServerIsRefused()
    {
        await Saga().Handle(Invoke(guildId: null), CancellationToken.None);

        _roleConfiguration.Verify(r => r.SaveServer(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnUnlinkedDiscordAccountIsToldToLinkFirst()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var reply = await Saga().Handle(Invoke(), CancellationToken.None);

        Assert.Contains("Link your Discord account", reply.Text);
        _roleConfiguration.Verify(r => r.SaveServer(It.IsAny<Guid>(), It.IsAny<ulong>(), It.IsAny<string>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     Moving a community to a different Discord hands back everything the old one gave out —
    ///     a role must not outlive the reason it was granted (D14).
    /// </summary>
    [Fact]
    public async Task RepointingToADifferentServerRevokesTheOldOneFirst()
    {
        _roleConfiguration.Setup(r => r.GetServer(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunityDiscordServerRecord(CommunityId, 999, "Old Server", Now));

        await Saga().Handle(Invoke(), CancellationToken.None);

        _discordRoles.Verify(d => d.RevokeAll(CommunityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RelinkingTheSameServerRevokesNothing()
    {
        _roleConfiguration.Setup(r => r.GetServer(CommunityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunityDiscordServerRecord(CommunityId, Guild, "Arrow Eclipse", Now));

        await Saga().Handle(Invoke(), CancellationToken.None);

        _discordRoles.Verify(d => d.RevokeAll(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
