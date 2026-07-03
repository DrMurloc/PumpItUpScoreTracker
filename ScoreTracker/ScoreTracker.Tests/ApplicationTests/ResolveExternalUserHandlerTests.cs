using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ResolveExternalUserHandlerTests
{
    private const string Provider = "PiuGame";
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly ResolveExternalUserHandler _handler;

    public ResolveExternalUserHandlerTests()
    {
        _handler = new ResolveExternalUserHandler(_mediator.Object, _users.Object);
    }

    [Fact]
    public async Task MatchesExistingUserByAnyAliasAndBackfillsUnclaimedAliases()
    {
        var existing = new UserBuilder().WithGameTag("SPOOKZ").Build();
        _users.Setup(u => u.GetUserByExternalLogin(Provider, "mbid:someone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _users.Setup(u => u.GetUserByExternalLogin(Provider, "card:123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _handler.Handle(
            new ResolveExternalUserCommand(Provider, new[] { "mbid:someone", "card:123" }, "SPOOKZ", "SPOOKZ", null),
            CancellationToken.None);

        Assert.False(result.IsNew);
        Assert.Equal(existing.Id, result.User.Id);
        _users.Verify(u => u.CreateExternalLogin(existing.Id, Provider, "mbid:someone", It.IsAny<CancellationToken>()),
            Times.Once);
        _users.Verify(u => u.CreateExternalLogin(existing.Id, Provider, "card:123", It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatesUserWithGameTagAndAvatarWhenNothingMatches()
    {
        var created = new UserBuilder().WithName("SPOOKZ").Build();
        var avatar = new Uri("https://piuimages.arroweclip.se/avatars/abc.png");
        _users.Setup(u => u.GetUserByExternalLogin(Provider, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mediator.Setup(m => m.Send(It.IsAny<CreateUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _handler.Handle(
            new ResolveExternalUserCommand(Provider, new[] { "mbid:someone", "card:123" }, "SPOOKZ", "SPOOKZ",
                avatar), CancellationToken.None);

        Assert.True(result.IsNew);
        Assert.Equal("SPOOKZ", result.User.GameTag!.Value.ToString());
        Assert.Equal(avatar, result.User.ProfileImage);
        _users.Verify(
            u => u.SaveUser(It.Is<User>(saved => saved.Id == created.Id && saved.ProfileImage == avatar),
                It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.CreateExternalLogin(created.Id, Provider, "mbid:someone", It.IsAny<CancellationToken>()),
            Times.Once);
        _users.Verify(u => u.CreateExternalLogin(created.Id, Provider, "card:123", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NeverRePointsAliasesOwnedByAnotherAccount()
    {
        var mine = new UserBuilder().Build();
        var somebodyElse = new UserBuilder().Build();
        _users.Setup(u => u.GetUserByExternalLogin(Provider, "mbid:someone", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mine);
        _users.Setup(u => u.GetUserByExternalLogin(Provider, "card:123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(somebodyElse);

        var result = await _handler.Handle(
            new ResolveExternalUserCommand(Provider, new[] { "mbid:someone", "card:123" }, "SPOOKZ", null, null),
            CancellationToken.None);

        Assert.Equal(mine.Id, result.User.Id);
        _users.Verify(
            u => u.CreateExternalLogin(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }
}
