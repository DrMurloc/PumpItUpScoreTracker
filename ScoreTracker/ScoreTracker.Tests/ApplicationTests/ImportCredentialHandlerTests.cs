using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Identity.Domain;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ImportCredentialHandlerTests
{
    private static Mock<ICurrentUserAccessor> CurrentUser(Guid userId)
    {
        var user = new UserBuilder().WithId(userId).Build();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(user);
        return currentUser;
    }

    [Fact]
    public async Task StoreReturnsTheKeyIdAndCiphertextFromTheProtector()
    {
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var protector = new Mock<IImportCredentialProtector>();
        protector.Setup(p => p.Protect(userId, "player1", "hunter2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((keyId, "cipher"));
        var handler = new StoreImportCredentialHandler(protector.Object, CurrentUser(userId).Object);

        var result = await handler.Handle(new StoreImportCredentialCommand("player1", "hunter2"),
            CancellationToken.None);

        Assert.Equal(keyId, result.KeyId);
        Assert.Equal("cipher", result.Ciphertext);
    }

    [Fact]
    public async Task RevealReturnsTheCredentialWhenUnlockSucceeds()
    {
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var protector = new Mock<IImportCredentialProtector>();
        protector.Setup(p => p.Unprotect(userId, keyId, "cipher", It.IsAny<CancellationToken>()))
            .ReturnsAsync(("player1", "hunter2"));
        var handler = new RevealImportCredentialHandler(protector.Object, CurrentUser(userId).Object);

        var result = await handler.Handle(new RevealImportCredentialQuery(keyId, "cipher"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("player1", result!.Username.Reveal());
        Assert.Equal("hunter2", result.Password.Reveal());
    }

    [Fact]
    public async Task RevealReturnsNullWhenTheCredentialCannotBeUnlocked()
    {
        var protector = new Mock<IImportCredentialProtector>();
        protector.Setup(p =>
                p.Unprotect(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CredentialUnlockException("nope"));
        var handler = new RevealImportCredentialHandler(protector.Object, CurrentUser(Guid.NewGuid()).Object);

        var result = await handler.Handle(new RevealImportCredentialQuery(Guid.NewGuid(), "cipher"),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ForgetDeletesTheCurrentUsersKey()
    {
        var userId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var keys = new Mock<IImportCredentialKeyStore>();
        var handler = new ForgetImportCredentialHandler(keys.Object, CurrentUser(userId).Object);

        await handler.Handle(new ForgetImportCredentialCommand(keyId), CancellationToken.None);

        keys.Verify(k => k.Delete(keyId, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgetAllDeletesEveryKeyForTheCurrentUser()
    {
        var userId = Guid.NewGuid();
        var keys = new Mock<IImportCredentialKeyStore>();
        var handler = new ForgetAllImportCredentialsHandler(keys.Object, CurrentUser(userId).Object);

        await handler.Handle(new ForgetAllImportCredentialsCommand(), CancellationToken.None);

        keys.Verify(k => k.DeleteAllForUser(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
