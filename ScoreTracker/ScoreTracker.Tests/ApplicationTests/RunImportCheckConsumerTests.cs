using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The completeness check's bus consumer: establish the job's user, run the body, and give the
///     per-user slot back however it ends.
/// </summary>
public sealed class RunImportCheckConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static ConsumeContext<RunImportCheckCommand> Context(bool deepScan = false)
    {
        var context = new Mock<ConsumeContext<RunImportCheckCommand>>();
        context.SetupGet(c => c.Message).Returns(new RunImportCheckCommand(UserId, MixEnum.Phoenix, "sid123",
            "card1", "TAG #1", deepScan));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private static RunImportCheckConsumer Build(Mock<IMediator> mediator, Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IImportConcurrencyGuard>? guard = null)
    {
        return new RunImportCheckConsumer(mediator.Object,
            (currentUser ?? new Mock<ICurrentUserAccessor>()).Object,
            (guard ?? new Mock<IImportConcurrencyGuard>()).Object);
    }

    [Fact]
    public async Task RunsTheCheckForTheMessagesUserAndSid()
    {
        var mediator = new Mock<IMediator>();

        await Build(mediator).Consume(Context(deepScan: true));

        mediator.Verify(m => m.Send(It.Is<ExecuteImportCheckCommand>(c =>
                c.UserId == UserId && c.Mix == MixEnum.Phoenix && c.Sid == "sid123" && c.CardId == "card1" &&
                c.DeepScan),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EstablishesTheJobsUserWithoutIssuingACookie()
    {
        var mediator = new Mock<IMediator>();
        var user = new UserBuilder().WithId(UserId).Build();
        mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var currentUser = new Mock<ICurrentUserAccessor>();

        await Build(mediator, currentUser).Consume(Context());

        // SetScopedUser, never SetCurrentUser: a request context can flow into a consumer, and
        // issuing a cookie there signs the live user out.
        currentUser.Verify(c => c.SetScopedUser(user), Times.Once);
        currentUser.Verify(c => c.SetCurrentUser(It.IsAny<Domain.Models.User>()), Times.Never);
    }

    [Fact]
    public async Task ReleasesThePerUserSlotWhenTheJobFinishes()
    {
        var guard = new Mock<IImportConcurrencyGuard>();

        await Build(new Mock<IMediator>(), guard: guard).Consume(Context());

        guard.Verify(g => g.End(UserId), Times.Once);
    }

    [Fact]
    public async Task ABadCredentialSurfacesAsAStatusErrorAndStillReleasesTheSlot()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialException());
        var guard = new Mock<IImportConcurrencyGuard>();

        await Build(mediator, guard: guard).Consume(Context());

        mediator.Verify(m => m.Publish(It.IsAny<ImportStatusErrorEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // Otherwise a wrong password locks the player out until the process restarts.
        guard.Verify(g => g.End(UserId), Times.Once);
    }

    [Fact]
    public async Task AnAccountWithNoGameProfileSurfacesAsAStatusError()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NoGameAccountAssociatedException());

        await Build(mediator).Consume(Context());

        mediator.Verify(m => m.Publish(It.IsAny<ImportStatusErrorEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
