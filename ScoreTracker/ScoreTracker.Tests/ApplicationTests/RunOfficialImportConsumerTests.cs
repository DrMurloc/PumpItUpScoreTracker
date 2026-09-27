using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
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

public sealed class RunOfficialImportConsumerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    private static ConsumeContext<RunOfficialImportCommand> Context(RunOfficialImportCommand message,
        CancellationToken cancellationToken = default)
    {
        var context = new Mock<ConsumeContext<RunOfficialImportCommand>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(cancellationToken);
        return context.Object;
    }

    private static RunOfficialImportCommand Message(Guid userId, bool includeBroken = false,
        ImportKind kind = ImportKind.Standard)
    {
        return new RunOfficialImportCommand(userId, MixEnum.Phoenix, "sid123", "card1", "TAG", includeBroken, kind);
    }

    private static Mock<IImportConcurrencyGuard> Guard(bool deepSlot = true)
    {
        var guard = new Mock<IImportConcurrencyGuard>();
        guard.Setup(g => g.TryBeginDeepScan()).Returns(deepSlot);
        return guard;
    }

    private static RunOfficialImportConsumer Build(Mock<IMediator> mediator,
        Mock<ICurrentUserAccessor>? currentUser = null, Mock<IImportConcurrencyGuard>? guard = null,
        Mock<IImportResultRepository>? results = null)
    {
        return new RunOfficialImportConsumer(mediator.Object,
            (currentUser ?? new Mock<ICurrentUserAccessor>()).Object,
            (guard ?? new Mock<IImportConcurrencyGuard>()).Object,
            (results ?? new Mock<IImportResultRepository>()).Object,
            FakeDateTime.At(Now).Object,
            NullLogger<RunOfficialImportConsumer>.Instance);
    }

    [Fact]
    public async Task RunsTheImportForTheMessagesUserAndSid()
    {
        var mediator = new Mock<IMediator>();
        var userId = Guid.NewGuid();

        await Build(mediator).Consume(Context(Message(userId, true)));

        mediator.Verify(m => m.Send(It.Is<ExecuteImportCommand>(c =>
                c.UserId == userId && c.Mix == MixEnum.Phoenix && c.Sid == "sid123" && c.CardId == "card1" &&
                c.IncludeBroken),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleasesTheImportSlotWhenTheJobFinishes()
    {
        var guard = new Mock<IImportConcurrencyGuard>();
        var userId = Guid.NewGuid();

        await Build(new Mock<IMediator>(), guard: guard).Consume(Context(Message(userId)));

        guard.Verify(g => g.End(userId), Times.Once);
    }

    [Fact]
    public async Task ReleasesTheImportSlotEvenWhenTheImportThrows()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialException("bad"));
        var guard = new Mock<IImportConcurrencyGuard>();
        var userId = Guid.NewGuid();

        await Build(mediator, guard: guard).Consume(Context(Message(userId)));

        guard.Verify(g => g.End(userId), Times.Once);
    }

    [Fact]
    public async Task EstablishesTheJobsUserForTheConsumerScope()
    {
        var userId = Guid.NewGuid();
        var user = new UserBuilder().WithId(userId).Build();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var currentUser = new Mock<ICurrentUserAccessor>();

        await Build(mediator, currentUser).Consume(Context(Message(userId)));

        // Scope-only (no cookie) so the live circuit that flowed in isn't signed out.
        currentUser.Verify(c => c.SetScopedUser(It.Is<User>(u => u.Id == userId)), Times.Once);
        currentUser.Verify(c => c.SetCurrentUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task InvalidCredentialAtTheSiteSurfacesAsAStatusError()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialException("bad"));
        var userId = Guid.NewGuid();

        await Build(mediator).Consume(Context(Message(userId)));

        mediator.Verify(m => m.Publish(It.Is<ImportStatusErrorEvent>(e =>
                e.UserId == userId && e.Error == "Invalid Login Information"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpensAResultBeforeTheRunAndClosesItCompleted()
    {
        var results = new Mock<IImportResultRepository>();
        var userId = Guid.NewGuid();

        await Build(new Mock<IMediator>(), results: results).Consume(Context(Message(userId)));

        results.Verify(r => r.Open(userId, MixEnum.Phoenix, ImportKind.Standard, "card1", Now,
            It.IsAny<CancellationToken>()), Times.Once);
        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.Completed, It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     The session is opened here rather than inside the import body so the run can point at
    ///     it, and the body is handed the same id — otherwise a single press would produce one
    ///     session for the run and a second for the body.
    /// </summary>
    [Fact]
    public async Task OpensOneSessionAndHandsItToTheImportBody()
    {
        var sessionId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<BeginScoreSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionId);
        var results = new Mock<IImportResultRepository>();
        var userId = Guid.NewGuid();

        await Build(mediator, results: results).Consume(Context(Message(userId)));

        mediator.Verify(m => m.Send(It.IsAny<BeginScoreSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(m => m.Send(It.Is<ExecuteImportCommand>(c => c.SessionId == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
        results.Verify(r => r.AttachSession(It.IsAny<Guid>(), sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    ///     The failure GODDISH reported on 2026-08-08: a 30s timeout out of the piugame client,
    ///     after the game tag and avatar had already been written. Before this it produced no log,
    ///     no event and no record — the player's import pulse simply spun forever.
    /// </summary>
    [Fact]
    public async Task APiuGameTimeoutIsRecordedAsTheirsAndTellsThePlayer()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout", new SocketException(10060)));
        var results = new Mock<IImportResultRepository>();
        var guard = new Mock<IImportConcurrencyGuard>();
        var userId = Guid.NewGuid();

        await Build(mediator, guard: guard, results: results).Consume(Context(Message(userId)));

        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.PiuGameError,
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Publish(It.Is<ImportStatusErrorEvent>(e => e.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
        guard.Verify(g => g.End(userId), Times.Once);
    }

    [Fact]
    public async Task ABugOnOurSideIsRecordedAsOurs()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NullReferenceException());
        var results = new Mock<IImportResultRepository>();

        await Build(mediator, results: results).Consume(Context(Message(Guid.NewGuid())));

        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.PiuScoresError,
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARejectedCredentialIsItsOwnOutcomeRatherThanASiteOutage()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialException("bad"));
        var results = new Mock<IImportResultRepository>();

        await Build(mediator, results: results).Consume(Context(Message(Guid.NewGuid())));

        // Folding this into PiuGameError would tell somebody with a mistyped password that the
        // site was down, and ask them to wait instead of fixing it.
        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.CredentialRejected,
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     A cancelled token is the process going away, not a fault. Leaving the row open is what
    ///     preserves the "never reported back" state — the one a deploy landing mid-import creates,
    ///     and the one a boolean outcome could not have expressed.
    /// </summary>
    [Fact]
    public async Task AShutdownLeavesTheRunUnfinishedRatherThanClaimingAnOutcome()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var results = new Mock<IImportResultRepository>();
        var guard = new Mock<IImportConcurrencyGuard>();

        await Build(mediator, guard: guard, results: results)
            .Consume(Context(Message(Guid.NewGuid()), cancelled.Token));

        results.Verify(r => r.Close(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<ImportOutcome>(),
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        guard.Verify(g => g.End(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task AnAccountWithNoGameProfileSurfacesAsAStatusError()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NoGameAccountAssociatedException());
        var results = new Mock<IImportResultRepository>();
        var userId = Guid.NewGuid();

        await Build(mediator, results: results).Consume(Context(Message(userId)));

        mediator.Verify(m => m.Publish(It.Is<ImportStatusErrorEvent>(e =>
                e.UserId == userId && e.Error == "No game profile is associated with this account yet."),
            It.IsAny<CancellationToken>()), Times.Once);
        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.CredentialRejected,
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     One consumer runs all three: the Import button, Import and check, and the deep scan are the
    ///     same run at different depths, recorded under the kind that was pressed because the three cost
    ///     the official site wildly different amounts.
    /// </summary>
    [Theory]
    [InlineData(ImportKind.Standard)]
    [InlineData(ImportKind.Check)]
    [InlineData(ImportKind.DeepScan)]
    public async Task TheRunIsRecordedUnderItsKindAndRunsAtThatDepth(ImportKind kind)
    {
        var mediator = new Mock<IMediator>();
        var results = new Mock<IImportResultRepository>();
        var userId = Guid.NewGuid();

        await Build(mediator, guard: Guard(), results: results).Consume(Context(Message(userId, kind: kind)));

        results.Verify(r => r.Open(userId, MixEnum.Phoenix, kind, "card1", Now, It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(m => m.Send(It.Is<ExecuteImportCommand>(c => c.Kind == kind),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ImportKind.Standard)]
    [InlineData(ImportKind.Check)]
    public async Task OnlyADeepScanTakesTheSiteWideSlot(ImportKind kind)
    {
        var guard = Guard();

        await Build(new Mock<IMediator>(), guard: guard).Consume(Context(Message(Guid.NewGuid(), kind: kind)));

        guard.Verify(g => g.TryBeginDeepScan(), Times.Never);
        guard.Verify(g => g.EndDeepScan(), Times.Never);
    }

    /// <summary>
    ///     Another deep scan is already walking piugame. This one waits rather than piling on, and says
    ///     so as an error: a status left the check panel spinning with nothing to stop it. It opens no
    ///     session, so the player's list gains no empty row.
    /// </summary>
    [Fact]
    public async Task ADeepScanWaitsRatherThanPilingOntoTheOnesAlreadyWalkingTheSite()
    {
        var mediator = new Mock<IMediator>();
        var guard = Guard(deepSlot: false);
        var results = new Mock<IImportResultRepository>();
        var userId = Guid.NewGuid();

        await Build(mediator, guard: guard, results: results)
            .Consume(Context(Message(userId, kind: ImportKind.DeepScan)));

        mediator.Verify(m => m.Send(It.IsAny<BeginScoreSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mediator.Verify(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        results.Verify(r => r.AttachSession(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mediator.Verify(m => m.Publish(It.Is<ImportStatusErrorEvent>(e => e.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
        results.Verify(r => r.Close(It.IsAny<Guid>(), Now, ImportOutcome.Completed, 0,
            It.IsAny<CancellationToken>()), Times.Once);
        guard.Verify(g => g.End(userId), Times.Once);
        guard.Verify(g => g.EndDeepScan(), Times.Never);
    }

    [Fact]
    public async Task TheSiteWideDeepScanSlotIsAlwaysReturned()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("piugame fell over"));
        var guard = Guard();

        await Build(mediator, guard: guard).Consume(Context(Message(Guid.NewGuid(), kind: ImportKind.DeepScan)));

        // A scan that dies mid-walk must not hold a site-wide slot until the process restarts.
        guard.Verify(g => g.EndDeepScan(), Times.Once);
    }

    [Fact]
    public async Task ARunWhoseRowCannotBeOpenedStillHandsThePlayersSlotBack()
    {
        // Opening the row is the run's first database write. Outside the try, a refusal there skipped
        // the finally and locked the player out of importing until the process restarted.
        var mediator = new Mock<IMediator>();
        var results = new Mock<IImportResultRepository>();
        results.Setup(r => r.Open(It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<ImportKind>(),
                It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("sql"));
        var guard = Guard();
        var userId = Guid.NewGuid();

        await Build(mediator, guard: guard, results: results).Consume(Context(Message(userId)));

        guard.Verify(g => g.End(userId), Times.Once);
        mediator.Verify(m => m.Publish(It.Is<ImportStatusErrorEvent>(e => e.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        results.Verify(r => r.Close(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<ImportOutcome>(),
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     A shutdown does not always arrive as an OperationCanceledException — a cancelled SQL command
    ///     throws its own type. Whatever the shape, a cancelled token means the process is going away,
    ///     so the row stays unfinished for startup recovery rather than being closed as our bug.
    /// </summary>
    [Fact]
    public async Task AShutdownInAnyShapeLeavesTheRunUnfinished()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ExecuteImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Operation cancelled by user."));
        var results = new Mock<IImportResultRepository>();

        await Build(mediator, results: results).Consume(Context(Message(Guid.NewGuid()), cancelled.Token));

        results.Verify(r => r.Close(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<ImportOutcome>(),
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(m => m.Publish(It.IsAny<ImportStatusErrorEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
