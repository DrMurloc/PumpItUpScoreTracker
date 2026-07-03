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

public sealed class DisconnectExternalProviderHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly DisconnectExternalProviderHandler _handler;

    public DisconnectExternalProviderHandlerTests()
    {
        _currentUser.Setup(c => c.User).Returns(new UserBuilder().WithId(_userId).Build());
        _handler = new DisconnectExternalProviderHandler(_currentUser.Object, _users.Object);
    }

    [Fact]
    public async Task RemovesEveryAliasRowOfTheProvider()
    {
        _users.Setup(u => u.GetExternalLogins(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExternalLoginRecord("Discord", "discord-id"),
                new ExternalLoginRecord("PiuGame", "mbid:someone"),
                new ExternalLoginRecord("PiuGame", "card:123")
            });

        await _handler.Handle(new DisconnectExternalProviderCommand("PiuGame"), CancellationToken.None);

        _users.Verify(u => u.RemoveExternalLogin(_userId, "PiuGame", "mbid:someone", It.IsAny<CancellationToken>()),
            Times.Once);
        _users.Verify(u => u.RemoveExternalLogin(_userId, "PiuGame", "card:123", It.IsAny<CancellationToken>()),
            Times.Once);
        _users.Verify(u => u.RemoveExternalLogin(_userId, "Discord", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ThrowsWhenDisconnectingTheOnlyProviderEvenAcrossMultipleAliasRows()
    {
        _users.Setup(u => u.GetExternalLogins(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExternalLoginRecord("PiuGame", "mbid:someone"),
                new ExternalLoginRecord("PiuGame", "card:123")
            });

        await Assert.ThrowsAsync<CannotRemoveLastExternalLoginException>(() =>
            _handler.Handle(new DisconnectExternalProviderCommand("PiuGame"), CancellationToken.None));

        _users.Verify(
            u => u.RemoveExternalLogin(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IgnoresProvidersNotOnTheAccount()
    {
        _users.Setup(u => u.GetExternalLogins(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ExternalLoginRecord("Discord", "discord-id") });

        await _handler.Handle(new DisconnectExternalProviderCommand("Google"), CancellationToken.None);

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

        await _handler.Handle(new DisconnectExternalProviderCommand("discord"), CancellationToken.None);

        _users.Verify(u => u.RemoveExternalLogin(_userId, "Discord", "discord-id", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
