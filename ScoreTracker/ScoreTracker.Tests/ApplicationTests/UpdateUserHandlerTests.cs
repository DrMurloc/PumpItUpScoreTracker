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
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class UpdateUserHandlerTests
{
    [Fact]
    public async Task SavesNewUserAndPublishesUpdatedEvent()
    {
        var existingUser = new UserBuilder()
            .WithName("Original")
            .WithGameTag("GAMETAG")
            .WithCountry("US")
            .Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(existingUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(existingUser);
        var bus = new Mock<IBus>();

        var handler = new UpdateUserHandler(users.Object, currentUser.Object, bus.Object);
        await handler.Handle(new UpdateUserCommand(Name.From("NewName"), false, Name.From("CA")),
            CancellationToken.None);

        users.Verify(u => u.SaveUser(
            It.Is<User>(saved => saved.Id == existingUser.Id
                                 && saved.Name == Name.From("NewName")
                                 && saved.IsPublic == false
                                 && saved.Country == Name.From("CA")
                                 && saved.GameTag == existingUser.GameTag),
            It.IsAny<CancellationToken>()), Times.Once);

        bus.Verify(b => b.Publish(It.Is<UserUpdatedEvent>(e => e.UserId == existingUser.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UsesDefaultProfileImageWhenExistingUserHasNone()
    {
        var current = new UserBuilder().Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(current);
        var bus = new Mock<IBus>();

        var handler = new UpdateUserHandler(users.Object, currentUser.Object, bus.Object);
        await handler.Handle(new UpdateUserCommand(Name.From("Name"), true, Name.From("US")),
            CancellationToken.None);

        users.Verify(u => u.SaveUser(It.Is<User>(saved => saved.ProfileImage != null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LockedUserCannotChangeName()
    {
        var actor = new UserBuilder().Build();
        var existing = new User(actor.Id, Name.From("locked-name"), true, null,
            new Uri("https://example.invalid/avatar.png"), null, true);
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(actor);
        var bus = new Mock<IBus>();

        var handler = new UpdateUserHandler(users.Object, currentUser.Object, bus.Object);

        await Assert.ThrowsAsync<ContentLockedException>(() =>
            handler.Handle(new UpdateUserCommand(Name.From("attempted-rename"), true, null), CancellationToken.None));

        users.Verify(u => u.SaveUser(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LockedUserCanStillUpdateOtherSettings()
    {
        var actor = new UserBuilder().Build();
        var lockedName = Name.From("locked-name");
        var existing = new User(actor.Id, lockedName, false, null,
            new Uri("https://example.invalid/avatar.png"), null, true);
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(actor);
        var bus = new Mock<IBus>();

        var handler = new UpdateUserHandler(users.Object, currentUser.Object, bus.Object);
        await handler.Handle(new UpdateUserCommand(lockedName, true, Name.From("USA")), CancellationToken.None);

        users.Verify(u => u.SaveUser(
            It.Is<User>(saved =>
                saved.Name == lockedName &&
                saved.IsPublic &&
                saved.Country == Name.From("USA") &&
                saved.IsContentLocked),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreservesContentLockFlagThroughUpdate()
    {
        var actor = new UserBuilder().Build();
        var existing = new UserBuilder().WithId(actor.Id).WithName("kept").Build();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(actor);
        var bus = new Mock<IBus>();

        var handler = new UpdateUserHandler(users.Object, currentUser.Object, bus.Object);
        await handler.Handle(new UpdateUserCommand(Name.From("renamed"), true, null), CancellationToken.None);

        users.Verify(u => u.SaveUser(
            It.Is<User>(saved => saved.Name == Name.From("renamed") && !saved.IsContentLocked),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelfUpdateDoesNotBumpClaimsInvalidatedAt()
    {
        var actor = new UserBuilder().Build();
        var stamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var existing = new User(actor.Id, Name.From("name"), true, null,
            new Uri("https://example.invalid/avatar.png"), null, false, stamp);
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUser(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(actor);
        var bus = new Mock<IBus>();

        var handler = new UpdateUserHandler(users.Object, currentUser.Object, bus.Object);
        await handler.Handle(new UpdateUserCommand(Name.From("name"), false, null), CancellationToken.None);

        users.Verify(u => u.SaveUser(
            It.Is<User>(saved => saved.ClaimsInvalidatedAt == stamp),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
