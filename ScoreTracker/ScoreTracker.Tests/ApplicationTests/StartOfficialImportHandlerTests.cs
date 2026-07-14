using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class StartOfficialImportHandlerTests
{
    private static (StartOfficialImportHandler handler, Mock<IOfficialSiteClient> site, Mock<IMediator> mediator,
        Mock<IBus> bus, Mock<IImportConcurrencyGuard> guard, Guid userId) Build()
    {
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.User).Returns(new UserBuilder().WithId(userId).Build());
        var site = new Mock<IOfficialSiteClient>();
        var mediator = new Mock<IMediator>();
        var bus = new Mock<IBus>();
        var guard = new Mock<IImportConcurrencyGuard>();
        // No import in flight by default; the AlreadyRunning test overrides this.
        guard.Setup(g => g.TryBegin(It.IsAny<Guid>())).Returns(true);
        var handler = new StartOfficialImportHandler(site.Object, mediator.Object, bus.Object, currentUser.Object,
            guard.Object);
        return (handler, site, mediator, bus, guard, userId);
    }

    [Fact]
    public async Task TypedCredentialSignsInAndPublishesTheBackgroundImportCarryingTheSid()
    {
        var (handler, site, _, bus, _, userId) = Build();
        site.Setup(s => s.SignIn(MixEnum.Phoenix, "player1", "hunter2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sid123");

        var result = await handler.Handle(new StartOfficialImportCommand(
            new TypedCredentialSource("player1", "hunter2"), MixEnum.Phoenix, "card1", "TAG", false, false),
            CancellationToken.None);

        Assert.Equal(ImportStartOutcome.Started, result.Outcome);
        bus.Verify(b => b.Publish(It.Is<RunOfficialImportCommand>(m =>
                m.UserId == userId && m.Mix == MixEnum.Phoenix && m.Sid.Reveal() == "sid123" && m.CardId == "card1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartedImportKeepsTheSlotForTheBackgroundJob()
    {
        var (handler, site, _, _, guard, userId) = Build();
        site.Setup(s => s.SignIn(MixEnum.Phoenix, "player1", "hunter2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sid123");

        await handler.Handle(new StartOfficialImportCommand(
            new TypedCredentialSource("player1", "hunter2"), MixEnum.Phoenix, "card1", "TAG", false, false),
            CancellationToken.None);

        // The consumer releases it when the scrape finishes — the Start handler must not.
        guard.Verify(g => g.End(userId), Times.Never);
    }

    [Fact]
    public async Task SecondConcurrentImportReturnsAlreadyRunningAndPublishesNothing()
    {
        var (handler, site, _, bus, guard, _) = Build();
        guard.Setup(g => g.TryBegin(It.IsAny<Guid>())).Returns(false);

        var result = await handler.Handle(new StartOfficialImportCommand(
            new TypedCredentialSource("player1", "hunter2"), MixEnum.Phoenix, "card1", "TAG", false, false),
            CancellationToken.None);

        Assert.Equal(ImportStartOutcome.AlreadyRunning, result.Outcome);
        bus.Verify(b => b.Publish(It.IsAny<RunOfficialImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        site.Verify(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StoredCredentialThatCannotUnlockReturnsUnlockFailedAndReleasesTheSlot()
    {
        var (handler, _, mediator, bus, guard, userId) = Build();
        var keyId = Guid.NewGuid();
        mediator.Setup(m => m.Send(It.Is<RevealImportCredentialQuery>(q => q.KeyId == keyId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RevealedImportCredential?)null);

        var result = await handler.Handle(new StartOfficialImportCommand(
            new StoredCredentialSource(keyId, "cipher"), MixEnum.Phoenix, "card1", "TAG", false, false),
            CancellationToken.None);

        Assert.Equal(ImportStartOutcome.CredentialUnlockFailed, result.Outcome);
        bus.Verify(b => b.Publish(It.IsAny<RunOfficialImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        // A pre-flight failure must free the slot so the user can retry immediately.
        guard.Verify(g => g.End(userId), Times.Once);
    }

    [Fact]
    public async Task StoredCredentialUnlocksThenSignsInAndPublishes()
    {
        var (handler, site, mediator, bus, _, _) = Build();
        var keyId = Guid.NewGuid();
        mediator.Setup(m => m.Send(It.Is<RevealImportCredentialQuery>(q => q.KeyId == keyId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevealedImportCredential("player1", "hunter2"));
        site.Setup(s => s.SignIn(MixEnum.Phoenix, "player1", "hunter2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sid123");

        var result = await handler.Handle(new StartOfficialImportCommand(
            new StoredCredentialSource(keyId, "cipher"), MixEnum.Phoenix, "card1", "TAG", false, false),
            CancellationToken.None);

        Assert.Equal(ImportStartOutcome.Started, result.Outcome);
        bus.Verify(b => b.Publish(It.Is<RunOfficialImportCommand>(m => m.Sid.Reveal() == "sid123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidCredentialsReturnsInvalidAndReleasesTheSlot()
    {
        var (handler, site, _, bus, guard, userId) = Build();
        site.Setup(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialException("no"));

        var result = await handler.Handle(new StartOfficialImportCommand(
            new TypedCredentialSource("player1", "wrong"), MixEnum.Phoenix, "card1", "TAG", false, false),
            CancellationToken.None);

        Assert.Equal(ImportStartOutcome.InvalidCredentials, result.Outcome);
        bus.Verify(b => b.Publish(It.IsAny<RunOfficialImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        guard.Verify(g => g.End(userId), Times.Once);
    }
}
