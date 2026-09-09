using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The angles that reach the reconcile from outside it. The rule itself is pinned in
///     <see cref="DiscordRoleSagaTests" />; what these assert is that each thing which can change
///     one of the four facts actually says so — and, for the two orderings that matter, that it
///     says so at the right moment.
/// </summary>
public sealed class DiscordRoleTriggerTests
{
    private static readonly Guid CommunityId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly Mock<IDiscordRoleService> _roles = new();

    [Fact]
    public async Task ATriggerForOneMemberSettlesOnlyThatMember()
    {
        var consumer = new DiscordRoleConsumer(_roles.Object, NullLogger<DiscordRoleConsumer>.Instance);

        await consumer.Consume(Message(new ReconcileDiscordRolesCommand(CommunityId, UserId)));

        _roles.Verify(r => r.ReconcileOne(CommunityId, UserId, It.IsAny<CancellationToken>()), Times.Once);
        _roles.Verify(r => r.ReconcileCommunity(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ATriggerWithNoMemberSettlesTheWholeCommunity()
    {
        var consumer = new DiscordRoleConsumer(_roles.Object, NullLogger<DiscordRoleConsumer>.Instance);

        await consumer.Consume(Message(new ReconcileDiscordRolesCommand(CommunityId)));

        _roles.Verify(r => r.ReconcileCommunity(CommunityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Discord being unreachable is not a reason to fail whatever the player was doing. The
    ///     sweep re-settles anyone this dropped.
    /// </summary>
    [Fact]
    public async Task ADiscordFailureDoesNotEscapeTheConsumer()
    {
        _roles.Setup(r => r.ReconcileOne(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("discord is down"));
        var consumer = new DiscordRoleConsumer(_roles.Object, NullLogger<DiscordRoleConsumer>.Instance);

        await consumer.Consume(Message(new ReconcileDiscordRolesCommand(CommunityId, UserId)));
    }

    /// <summary>
    ///     The ordering the whole grant table exists for. Identity deletes the external login on
    ///     the first purge pass, so if the row went before the roles came off there would be
    ///     nothing left that knows which Discord account to take them from.
    /// </summary>
    [Fact]
    public async Task PurgingTakesTheRolesBackBeforeItDeletesTheRowThatNamesThem()
    {
        var order = new List<string>();
        var purge = new Mock<IAccountPurgeRepository>();
        _roles.Setup(r => r.RemoveAllForUser(UserId, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("roles")).Returns(Task.CompletedTask);
        purge.Setup(p => p.DeleteAllForUser(UserId, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("rows")).Returns(Task.CompletedTask);

        var consumer = new AccountPurgeConsumer(purge.Object, _roles.Object,
            NullLogger<AccountPurgeConsumer>.Instance);
        await consumer.Consume(Message(new AccountPurgeStartedEvent(UserId)));

        Assert.Equal(new[] { "roles", "rows" }, order);
    }

    /// <summary>
    ///     A purge must complete even when Discord does not answer — the account's data is deleted
    ///     either way, and the event re-fires daily for a week while the grant row still exists.
    /// </summary>
    [Fact]
    public async Task APurgeStillDeletesTheDataWhenDiscordIsUnreachable()
    {
        var purge = new Mock<IAccountPurgeRepository>();
        _roles.Setup(r => r.RemoveAllForUser(UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("discord is down"));

        var consumer = new AccountPurgeConsumer(purge.Object, _roles.Object,
            NullLogger<AccountPurgeConsumer>.Instance);
        await consumer.Consume(Message(new AccountPurgeStartedEvent(UserId)));

        purge.Verify(p => p.DeleteAllForUser(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConsumeContext<T> Message<T>(T message) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }
}
