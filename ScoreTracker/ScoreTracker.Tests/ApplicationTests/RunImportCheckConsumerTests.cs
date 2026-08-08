using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
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

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     Moq hands back a null Task for an unstubbed Task&lt;T&gt;, and the consumer awaits this
    ///     one. Installed at construction rather than inside Build so a test's own setup — which
    ///     runs after — still wins.
    /// </summary>
    private static Mock<IMediator> Mediator()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportCheckRun(Guid.NewGuid(), 0));
        return mediator;
    }

    private static RunImportCheckConsumer Build(Mock<IMediator> mediator, Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IImportConcurrencyGuard>? guard = null, Mock<IImportResultRepository>? results = null)
    {
        return new RunImportCheckConsumer(mediator.Object,
            (currentUser ?? new Mock<ICurrentUserAccessor>()).Object,
            (guard ?? new Mock<IImportConcurrencyGuard>()).Object,
            (results ?? new Mock<IImportResultRepository>()).Object,
            FakeDateTime.At(Now).Object,
            NullLogger<RunImportCheckConsumer>.Instance);
    }

    [Fact]
    public async Task RunsTheCheckForTheMessagesUserAndSid()
    {
        var mediator = Mediator();

        await Build(mediator).Consume(Context(deepScan: true));

        mediator.Verify(m => m.Send(It.Is<ExecuteImportCheckCommand>(c =>
                c.UserId == UserId && c.Mix == MixEnum.Phoenix && c.Sid == "sid123" && c.CardId == "card1" &&
                c.DeepScan),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EstablishesTheJobsUserWithoutIssuingACookie()
    {
        var mediator = Mediator();
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

        await Build(Mediator(), guard: guard).Consume(Context());

        guard.Verify(g => g.End(UserId), Times.Once);
    }

    [Fact]
    public async Task ABadCredentialSurfacesAsAStatusErrorAndStillReleasesTheSlot()
    {
        var mediator = Mediator();
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
        var mediator = Mediator();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NoGameAccountAssociatedException());

        await Build(mediator).Consume(Context());

        mediator.Verify(m => m.Publish(It.IsAny<ImportStatusErrorEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ADeepScanIsRecordedAsOneAndACheckIsNot()
    {
        var results = new Mock<IImportResultRepository>();

        await Build(Mediator(), results: results).Consume(Context(deepScan: true));
        await Build(Mediator(), results: results).Consume(Context());

        // A deep scan walks every page and a check counts levels first, so the two cost the site
        // wildly different amounts — telling them apart is the point of recording the kind.
        results.Verify(r => r.Open(UserId, MixEnum.Phoenix, ImportKind.DeepScan, "card1", Now,
            It.IsAny<CancellationToken>()), Times.Once);
        results.Verify(r => r.Open(UserId, MixEnum.Phoenix, ImportKind.Check, "card1", Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARefusedDeepScanAttachesNoSession()
    {
        var mediator = Mediator();
        // Null is the saga saying it never opened one, because the site-wide deep-scan slot was
        // taken. Attaching anything here would point the run at somebody else's session.
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportCheckRun(null, 0));
        var results = new Mock<IImportResultRepository>();

        await Build(mediator, results: results).Consume(Context(deepScan: true));

        results.Verify(r => r.AttachSession(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.Completed, It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task APiuGameTimeoutClosesTheRunAsTheirsAndTellsThePlayer()
    {
        var mediator = Mediator();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));
        var results = new Mock<IImportResultRepository>();
        var guard = new Mock<IImportConcurrencyGuard>();

        await Build(mediator, guard: guard, results: results).Consume(Context(deepScan: true));

        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.PiuGameError,
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<ImportStatusErrorEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        guard.Verify(g => g.End(UserId), Times.Once);
    }
}
