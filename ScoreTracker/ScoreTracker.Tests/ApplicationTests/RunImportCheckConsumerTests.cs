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
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The completeness check's bus consumer: establish the job's user, take a deep scan's
///     site-wide slot, open the run's session and point the run at it, run the body, and give
///     both slots back however it ends.
/// </summary>
public sealed class RunImportCheckConsumerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static ConsumeContext<RunImportCheckCommand> Context(bool deepScan = false,
        bool includeBroken = false, CancellationToken cancellationToken = default)
    {
        var context = new Mock<ConsumeContext<RunImportCheckCommand>>();
        context.SetupGet(c => c.Message).Returns(new RunImportCheckCommand(UserId, MixEnum.Phoenix, "sid123",
            "card1", "TAG #1", deepScan, includeBroken));
        context.SetupGet(c => c.CancellationToken).Returns(cancellationToken);
        return context.Object;
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     A mediator whose run opens <paramref name="sessionId" /> — a fresh one when none is
    ///     given. Installed at construction rather than inside Build so a test's own setup — which
    ///     runs after — still wins.
    /// </summary>
    private static Mock<IMediator> Mediator(Guid? sessionId = null)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<BeginScoreSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionId ?? Guid.NewGuid());
        return mediator;
    }

    /// <summary>A guard with a site-wide deep-scan slot free, unless told otherwise.</summary>
    private static Mock<IImportConcurrencyGuard> Guard(bool deepSlot = true)
    {
        var guard = new Mock<IImportConcurrencyGuard>();
        guard.Setup(g => g.TryBeginDeepScan()).Returns(deepSlot);
        return guard;
    }

    private static Mock<IImportResultRepository> Results(Guid resultId)
    {
        var results = new Mock<IImportResultRepository>();
        results.Setup(r => r.Open(It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<ImportKind>(),
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultId);
        return results;
    }

    private static RunImportCheckConsumer Build(Mock<IMediator> mediator, Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IImportConcurrencyGuard>? guard = null, Mock<IImportResultRepository>? results = null)
    {
        return new RunImportCheckConsumer(mediator.Object,
            (currentUser ?? new Mock<ICurrentUserAccessor>()).Object,
            (guard ?? Guard()).Object,
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

    /// <summary>
    ///     The session carries the tag and card the run pulled from — what the Undo page shows —
    ///     and the check saves into the same session the run points at, so the session recovery
    ///     would replay is the one holding the scores.
    /// </summary>
    [Fact]
    public async Task OpensOneSessionPointsTheRunAtItAndHandsItToTheCheck()
    {
        var resultId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var mediator = Mediator(sessionId);
        var results = Results(resultId);

        await Build(mediator, results: results).Consume(Context(deepScan: true));

        mediator.Verify(m => m.Send(It.Is<BeginScoreSessionCommand>(c =>
                c.UserId == UserId && c.Mix == MixEnum.Phoenix &&
                c.Source == ScoreJournalEntry.OfficialImportSource && c.AccountTag == "TAG #1" &&
                c.CardId == "card1"),
            It.IsAny<CancellationToken>()), Times.Once);
        results.Verify(r => r.AttachSession(resultId, sessionId, It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.Is<ExecuteImportCheckCommand>(c => c.SessionId == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     A deploy landing mid-check. Recovery reaches a run only through its session, so a run cut
    ///     off before it says which session it saved into is invisible to it: never closed, never
    ///     disclosed, and the scores it had already saved never get their highlights, lamps or card.
    ///     A deep scan walks every best-score page, which makes it the run a restart is most likely
    ///     to land in.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AShutdownMidCheckLeavesTheRunPointingAtItsSession(bool deepScan)
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var resultId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var mediator = Mediator(sessionId);
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var results = Results(resultId);

        await Build(mediator, results: results)
            .Consume(Context(deepScan, cancellationToken: cancelled.Token));

        results.Verify(r => r.AttachSession(resultId, sessionId, It.IsAny<CancellationToken>()), Times.Once);
        // Still left open: closing it is the boot pass's verdict, reached once the process is back.
        results.Verify(r => r.Close(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<ImportOutcome>(),
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ADeepScanWaitsRatherThanPilingOntoTheOnesAlreadyWalkingTheSite()
    {
        var mediator = Mediator();

        await Build(mediator, guard: Guard(deepSlot: false)).Consume(Context(deepScan: true));

        mediator.Verify(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // Told why, rather than left watching an import that never starts.
        mediator.Verify(m => m.Publish(It.IsAny<ImportStatusUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ARefusedDeepScanOpensNoSession()
    {
        var mediator = Mediator();
        var results = new Mock<IImportResultRepository>();

        await Build(mediator, guard: Guard(deepSlot: false), results: results).Consume(Context(deepScan: true));

        // The slot is settled before the session opens, so losing the race leaves no empty row in
        // the player's sessions list — and nothing for the run to point at.
        mediator.Verify(m => m.Send(It.IsAny<BeginScoreSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        results.Verify(r => r.AttachSession(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.Completed, 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TheGlobalDeepScanSlotIsAlwaysReturned()
    {
        var mediator = Mediator();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("piugame fell over"));
        var guard = Guard();

        await Build(mediator, guard: guard).Consume(Context(deepScan: true));

        // A scan that dies mid-walk must not hold a site-wide slot until the process restarts.
        guard.Verify(g => g.EndDeepScan(), Times.Once);
    }

    [Fact]
    public async Task APlainCheckNeverTouchesTheDeepScanSlot()
    {
        var guard = Guard();

        await Build(Mediator(), guard: guard).Consume(Context());

        // The census never waits on a deep scan, and it must not release a slot it never took —
        // that would let one more walk start than the site-wide cap allows.
        guard.Verify(g => g.TryBeginDeepScan(), Times.Never);
        guard.Verify(g => g.EndDeepScan(), Times.Never);
    }

    [Fact]
    public async Task APiuGameTimeoutClosesTheRunAsTheirsAndTellsThePlayer()
    {
        var mediator = Mediator();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCheckCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));
        var results = new Mock<IImportResultRepository>();
        var guard = Guard();

        await Build(mediator, guard: guard, results: results).Consume(Context(deepScan: true));

        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.PiuGameError,
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<ImportStatusErrorEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        guard.Verify(g => g.End(UserId), Times.Once);
    }
}
