using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SetUserContentLockHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713");
    private static readonly DateTimeOffset ActionTime = new(2026, 4, 26, 12, 0, 0, TimeSpan.Zero);

    private static (SetUserContentLockHandler handler,
        Mock<IUserRepository> users,
        Mock<IBus> bus) Build(User actor)
    {
        var users = new Mock<IUserRepository>();
        var bus = new Mock<IBus>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(actor);
        var clock = FakeDateTime.At(ActionTime);
        return (new SetUserContentLockHandler(users.Object, currentUser.Object, clock.Object, bus.Object), users, bus);
    }

    [Fact]
    public async Task ThrowsWhenActorIsNotAdmin()
    {
        var (handler, _, _) = Build(new UserBuilder().WithId(Guid.NewGuid()));

        await Assert.ThrowsAsync<NotAuthorizedException>(() =>
            handler.Handle(new SetUserContentLockCommand(Guid.NewGuid(), true, null), CancellationToken.None));
    }

    [Fact]
    public async Task ThrowsWhenTargetUserNotFound()
    {
        var (handler, users, _) = Build(new UserBuilder().WithId(AdminId));
        var targetId = Guid.NewGuid();
        users.Setup(u => u.GetUser(targetId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new SetUserContentLockCommand(targetId, true, null), CancellationToken.None));
    }

    [Fact]
    public async Task LockingWithGameTagRenamesToGameTagAndSetsFlag()
    {
        var (handler, users, bus) = Build(new UserBuilder().WithId(AdminId));
        var target = new UserBuilder()
            .WithName("abusive-name")
            .WithGameTag("clean-tag")
            .Build();
        users.Setup(u => u.GetUser(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await handler.Handle(new SetUserContentLockCommand(target.Id, true, null), CancellationToken.None);

        users.Verify(u => u.SaveUser(
            It.Is<User>(saved =>
                saved.Id == target.Id &&
                saved.Name == Name.From("clean-tag") &&
                saved.IsContentLocked &&
                saved.ClaimsInvalidatedAt == ActionTime),
            It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(b => b.Publish(It.Is<UserUpdatedEvent>(e => e.UserId == target.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LockingWithoutGameTagUsesOverrideName()
    {
        var (handler, users, _) = Build(new UserBuilder().WithId(AdminId));
        var target = new UserBuilder().WithName("abusive-name").Build();
        users.Setup(u => u.GetUser(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await handler.Handle(new SetUserContentLockCommand(target.Id, true, Name.From("admin-chosen")),
            CancellationToken.None);

        users.Verify(u => u.SaveUser(
            It.Is<User>(saved =>
                saved.Name == Name.From("admin-chosen") &&
                saved.IsContentLocked),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LockingWithoutGameTagOrOverrideThrows()
    {
        var (handler, users, _) = Build(new UserBuilder().WithId(AdminId));
        var target = new UserBuilder().WithName("abusive-name").Build();
        users.Setup(u => u.GetUser(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await Assert.ThrowsAsync<InvalidNameException>(() =>
            handler.Handle(new SetUserContentLockCommand(target.Id, true, null), CancellationToken.None));
        users.Verify(u => u.SaveUser(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OverrideNameWinsOverGameTagWhenProvided()
    {
        var (handler, users, _) = Build(new UserBuilder().WithId(AdminId));
        var target = new UserBuilder().WithName("abusive").WithGameTag("would-be-default").Build();
        users.Setup(u => u.GetUser(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await handler.Handle(new SetUserContentLockCommand(target.Id, true, Name.From("admin-pick")),
            CancellationToken.None);

        users.Verify(u => u.SaveUser(It.Is<User>(saved => saved.Name == Name.From("admin-pick")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnlockingFlipsFlagAndPreservesName()
    {
        var (handler, users, bus) = Build(new UserBuilder().WithId(AdminId));
        var target = new User(Guid.NewGuid(), Name.From("kept-name"), true, Name.From("any-tag"),
            new Uri("https://example.invalid/avatar.png"), null, true);
        users.Setup(u => u.GetUser(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await handler.Handle(new SetUserContentLockCommand(target.Id, false, null), CancellationToken.None);

        users.Verify(u => u.SaveUser(
            It.Is<User>(saved =>
                saved.Id == target.Id &&
                saved.Name == Name.From("kept-name") &&
                !saved.IsContentLocked &&
                saved.ClaimsInvalidatedAt == ActionTime),
            It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(b => b.Publish(It.Is<UserUpdatedEvent>(e => e.UserId == target.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
