using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class LinkExternalAliasesHandlerTests
{
    private const string Provider = "PiuGame";
    private readonly User _me = new UserBuilder().Build();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly LinkExternalAliasesHandler _handler;

    public LinkExternalAliasesHandlerTests()
    {
        _currentUser.Setup(c => c.User).Returns(_me);
        _handler = new LinkExternalAliasesHandler(_currentUser.Object, _users.Object);
    }

    [Fact]
    public async Task LinksUnclaimedAliasesToTheCurrentAccount()
    {
        _users.Setup(u => u.GetUserByExternalLogin(Provider, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _handler.Handle(
            new LinkExternalAliasesCommand(Provider, new[] { "mbid:someone", "card:123" }), CancellationToken.None);

        Assert.Equal(ExternalLinkResult.Linked, result.Result);
        _users.Verify(u => u.CreateExternalLogin(_me.Id, Provider, "mbid:someone", It.IsAny<CancellationToken>()),
            Times.Once);
        _users.Verify(u => u.CreateExternalLogin(_me.Id, Provider, "card:123", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReportsAlreadyLinkedWhenEveryAliasIsMine()
    {
        _users.Setup(u => u.GetUserByExternalLogin(Provider, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_me);

        var result = await _handler.Handle(
            new LinkExternalAliasesCommand(Provider, new[] { "mbid:someone" }), CancellationToken.None);

        Assert.Equal(ExternalLinkResult.AlreadyLinked, result.Result);
        _users.Verify(
            u => u.CreateExternalLogin(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinksNothingWhenAnyAliasBelongsToAnotherAccount()
    {
        var somebodyElse = new UserBuilder().Build();
        _users.Setup(u => u.GetUserByExternalLogin(Provider, "mbid:someone", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _users.Setup(u => u.GetUserByExternalLogin(Provider, "card:123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(somebodyElse);

        var result = await _handler.Handle(
            new LinkExternalAliasesCommand(Provider, new[] { "mbid:someone", "card:123" }), CancellationToken.None);

        Assert.Equal(ExternalLinkResult.ConflictingAccount, result.Result);
        Assert.Equal(somebodyElse.Id, result.ConflictingUserId);
        _users.Verify(
            u => u.CreateExternalLogin(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }
}
