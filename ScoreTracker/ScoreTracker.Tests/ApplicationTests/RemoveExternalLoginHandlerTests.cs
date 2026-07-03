using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RemoveExternalLoginHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly RemoveExternalLoginHandler _handler;

    public RemoveExternalLoginHandlerTests()
    {
        _currentUser.Setup(c => c.User).Returns(new UserBuilder().WithId(_userId).Build());
        _handler = new RemoveExternalLoginHandler(_currentUser.Object, _users.Object);
    }

    [Fact]
    public async Task RemovesLoginWhenAnotherMethodRemains()
    {
        _users.Setup(u => u.GetExternalLogins(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExternalLoginRecord("Discord", "discord-id"),
                new ExternalLoginRecord("Google", "google-id")
            });

        await _handler.Handle(new RemoveExternalLoginCommand("Discord", "discord-id"), CancellationToken.None);

        _users.Verify(u => u.RemoveExternalLogin(_userId, "Discord", "discord-id", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ThrowsAndRemovesNothingWhenTargetIsTheLastLogin()
    {
        _users.Setup(u => u.GetExternalLogins(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ExternalLoginRecord("Discord", "discord-id") });

        await Assert.ThrowsAsync<CannotRemoveLastExternalLoginException>(() =>
            _handler.Handle(new RemoveExternalLoginCommand("Discord", "discord-id"), CancellationToken.None));

        _users.Verify(
            u => u.RemoveExternalLogin(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IgnoresLoginsThatAreNotOnTheAccount()
    {
        _users.Setup(u => u.GetExternalLogins(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ExternalLoginRecord("Discord", "discord-id") });

        await _handler.Handle(new RemoveExternalLoginCommand("Google", "someone-elses-id"), CancellationToken.None);

        _users.Verify(
            u => u.RemoveExternalLogin(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MatchesProviderNamesCaseInsensitively()
    {
        _users.Setup(u => u.GetExternalLogins(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExternalLoginRecord("Discord", "discord-id"),
                new ExternalLoginRecord("Google", "google-id")
            });

        await _handler.Handle(new RemoveExternalLoginCommand("discord", "discord-id"), CancellationToken.None);

        _users.Verify(u => u.RemoveExternalLogin(_userId, "Discord", "discord-id", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
