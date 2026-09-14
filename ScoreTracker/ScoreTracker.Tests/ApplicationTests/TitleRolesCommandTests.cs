using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     <c>/piu roles off</c> and <c>/piu roles on</c>: the player's own say-so on a community's
///     title roles (docs/design/discord-role-management.md D22–D25). The command writes a fact and
///     runs the ordinary reconcile; these pin what it writes, whom it settles, and what it says.
/// </summary>
public sealed class TitleRolesCommandTests
{
    private const ulong Guild = 200;
    private const ulong Invoker = 300;
    private static readonly Guid CommunityId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SecondCommunityId = Guid.Parse("57575757-5757-5757-5757-575757575757");
    private static readonly Guid UserId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IBotClient> _bot = new();
    private readonly Mock<IBus> _bus = new();
    private readonly Mock<ICommunityRepository> _communities = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IDiscordRoleService> _discordRoles = new();
    private readonly Mock<IDiscordFeedSubscriptionRepository> _feeds = new();
    private readonly Mock<ILocalizedTextAccessor> _localizer = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IDiscordRoleRepository> _roleConfiguration = new();

    public TitleRolesCommandTests()
    {
        _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>()))
            .Returns((string? _, string key) => key);
        _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string? _, string key, object[] args) => string.Format(key, args));
        _mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().WithId(UserId).WithName("Jon").Build());

        Designated(new CommunityDiscordServerRecord(CommunityId, Guild, "Arrow Eclipse", Now, "Korea"));
        _roleConfiguration.Setup(r => r.DeleteOptOut(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private void Designated(params CommunityDiscordServerRecord[] servers) =>
        _roleConfiguration.Setup(r => r.GetServersByGuild(Guild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(servers);

    private BotCommandSaga Saga() =>
        new(_bot.Object, _communities.Object, _feeds.Object, _mediator.Object, _currentUser.Object,
            _localizer.Object, _roleConfiguration.Object, _discordRoles.Object,
            FakeDateTime.At(Now).Object, _bus.Object, NullLogger<BotCommandSaga>.Instance);

    private static HandleBotInteractionCommand Invoke(string leaf, ulong? guildId = Guild) =>
        new(new BotInteraction(new[] { "roles", leaf }, new Dictionary<string, string>(),
            ChannelId: 100, GuildId: guildId, UserId: Invoker, UserDisplayName: "Tester",
            InvokerCanManageChannels: false));

    private void VerifyNothingRecorded()
    {
        _roleConfiguration.Verify(r => r.SaveOptOut(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _roleConfiguration.Verify(r => r.DeleteOptOut(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _discordRoles.Verify(d => d.ReconcileOne(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- off --------------------------------------------------------------------------------

    /// <summary>
    ///     The command writes the fact and then runs the same reconcile the sweep runs, so the
    ///     roles are off before the reply lands (D18, D23) — nothing waits on a nightly pass.
    /// </summary>
    [Fact]
    public async Task TurningOffRecordsTheOptOutAndSettlesThePlayerOnTheSpot()
    {
        var reply = await Saga().Handle(Invoke("off"), CancellationToken.None);

        _roleConfiguration.Verify(r => r.SaveOptOut(CommunityId, UserId, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        _discordRoles.Verify(d => d.ReconcileOne(CommunityId, UserId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Jon", reply.Text);
        Assert.Contains("Korea", reply.Text);
        Assert.Contains("/piu roles on", reply.Text);
    }

    /// <summary>
    ///     Two communities pointing at one Discord is allowed, and the player should not have to
    ///     know which site community is behind the roles: one command covers all of them (D22).
    /// </summary>
    [Fact]
    public async Task TurningOffCoversEveryCommunityThatDesignatesTheServer()
    {
        Designated(new CommunityDiscordServerRecord(CommunityId, Guild, "Arrow Eclipse", Now, "Korea"),
            new CommunityDiscordServerRecord(SecondCommunityId, Guild, "Arrow Eclipse", Now, "World"));

        var reply = await Saga().Handle(Invoke("off"), CancellationToken.None);

        _roleConfiguration.Verify(r => r.SaveOptOut(CommunityId, UserId, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        _roleConfiguration.Verify(r => r.SaveOptOut(SecondCommunityId, UserId, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        _discordRoles.Verify(d => d.ReconcileOne(SecondCommunityId, UserId, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Contains("Korea, World", reply.Text);
    }

    /// <summary>
    ///     A role above the bot cannot come off, and Discord says so by throwing. The preference
    ///     must not depend on that — the row stands, and the reply says what is left for an admin.
    /// </summary>
    [Fact]
    public async Task TurningOffStillStandsWhenARoleCannotComeOff()
    {
        _discordRoles.Setup(d => d.ReconcileOne(CommunityId, UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Missing Permissions"));

        var reply = await Saga().Handle(Invoke("off"), CancellationToken.None);

        _roleConfiguration.Verify(r => r.SaveOptOut(CommunityId, UserId, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Contains("couldn't come off", reply.Text);
        Assert.Contains("Jon", reply.Text);
    }

    // ---- on ---------------------------------------------------------------------------------

    [Fact]
    public async Task TurningOnLiftsTheOptOutAndHandsTheRolesBack()
    {
        var reply = await Saga().Handle(Invoke("on"), CancellationToken.None);

        _roleConfiguration.Verify(r => r.DeleteOptOut(CommunityId, UserId, It.IsAny<CancellationToken>()),
            Times.Once);
        _discordRoles.Verify(d => d.ReconcileOne(CommunityId, UserId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("back on", reply.Text);
        Assert.Contains("Korea", reply.Text);
    }

    [Fact]
    public async Task TurningOnWithNothingToLiftSaysSo()
    {
        _roleConfiguration.Setup(r => r.DeleteOptOut(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var reply = await Saga().Handle(Invoke("on"), CancellationToken.None);

        Assert.Contains("already on", reply.Text);
    }

    // ---- refusals ---------------------------------------------------------------------------

    [Fact]
    public async Task OutsideAServerNothingIsRecorded()
    {
        var reply = await Saga().Handle(Invoke("off", guildId: null), CancellationToken.None);

        Assert.Contains("Run this in", reply.Text);
        VerifyNothingRecorded();
    }

    [Fact]
    public async Task AServerNoCommunityDesignatesIsRefused()
    {
        Designated();

        var reply = await Saga().Handle(Invoke("off"), CancellationToken.None);

        Assert.Contains("doesn't hand out title roles", reply.Text);
        VerifyNothingRecorded();
    }

    [Fact]
    public async Task AnUnlinkedDiscordAccountIsToldToLinkFirst()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var reply = await Saga().Handle(Invoke("off"), CancellationToken.None);

        Assert.Contains("Link your Discord account", reply.Text);
        VerifyNothingRecorded();
    }
}
