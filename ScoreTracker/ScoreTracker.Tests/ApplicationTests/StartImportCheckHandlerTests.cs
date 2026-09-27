using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Starting a completeness check on the request circuit: what it refuses before anything is queued
///     or spent, and that what it queues is the one import run at a deeper depth. The run itself is
///     the import's (OfficialLeaderboardSagaTests, RunOfficialImportConsumerTests).
/// </summary>
public sealed class StartImportCheckHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartingHandsTheScrapeToTheBusAndKeepsThePasswordOnTheCircuit()
    {
        var bus = new Mock<IBus>();

        var result = await Build(bus: bus).Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.Started, result.Outcome);
        bus.Verify(b => b.Publish(
            It.Is<RunOfficialImportCommand>(c => c.UserId == UserId && c.Kind == ImportKind.Check),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABadPasswordIsCaughtOnTheCircuitBeforeAnythingIsQueuedOrSpent()
    {
        var bus = new Mock<IBus>();
        var mediator = Mediator();
        var site = new Mock<IOfficialSiteClient>();
        site.Setup(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidCredentialException());

        var result = await Build(bus: bus, mediator: mediator, site: site)
            .Handle(Start(deepScan: true), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.InvalidCredentials, result.Outcome);
        bus.Verify(b => b.Publish(It.IsAny<RunOfficialImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        // A mistyped password must not cost one of the month's three scans.
        mediator.Verify(m => m.Send(It.IsAny<SpendDeepScanCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASecondCheckIsRefusedWhileOneIsInFlight()
    {
        var result = await Build(guard: Guard(userSlot: false)).Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.AlreadyRunning, result.Outcome);
    }

    [Fact]
    public async Task APreFlightFailureHandsTheUsersSlotBack()
    {
        var guard = Guard();
        var site = new Mock<IOfficialSiteClient>();
        site.Setup(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidCredentialException());

        await Build(guard: guard, site: site).Handle(Start(), CancellationToken.None);

        // Otherwise a mistyped password locks the player out of retrying until the process restarts.
        guard.Verify(g => g.End(UserId), Times.Once);
    }

    [Fact]
    public async Task ADeepScanSpendsOneOfTheMonthsAllowanceAndAPlainCheckSpendsNothing()
    {
        var mediator = Mediator();
        var saga = Build(mediator: mediator);

        var deep = await saga.Handle(Start(deepScan: true), CancellationToken.None);
        await saga.Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.Started, deep.Outcome);
        Assert.Equal(2, deep.DeepScansLeft);
        mediator.Verify(m => m.Send(It.Is<SpendDeepScanCommand>(c => c.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnEmptyAllowanceRefusesTheDeepScanButNotTheCensus()
    {
        var mediator = Mediator(scansLeft: 0);
        var saga = Build(mediator: mediator);

        var deep = await saga.Handle(Start(deepScan: true), CancellationToken.None);
        var census = await saga.Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.NoDeepScansLeft, deep.Outcome);
        // The allowance rations "walk everything", never the cheap per-level check.
        Assert.Equal(ImportCheckStartOutcome.Started, census.Outcome);
    }

    [Fact]
    public async Task LosingTheAllowanceRaceRefusesRatherThanRunningForFree()
    {
        // The balance read said one was left, but another tab spent it before this one asked.
        var mediator = Mediator(scansLeft: 1, spendGranted: false);

        var result = await Build(mediator: mediator).Handle(Start(deepScan: true), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.NoDeepScansLeft, result.Outcome);
    }

    [Fact]
    public async Task ADeepScanIsQueuedAsTheImportAtItsDeepestDepth()
    {
        var bus = new Mock<IBus>();

        await Build(bus: bus).Handle(Start(deepScan: true, includeBroken: true), CancellationToken.None);

        bus.Verify(b => b.Publish(
            It.Is<RunOfficialImportCommand>(c => c.UserId == UserId && c.Kind == ImportKind.DeepScan &&
                                                 c.IncludeBroken && c.CardId == "card" &&
                                                 c.ExpectedGameTag == "TAG #1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACheckWaitsOutTheCooldownLikeTheImportButton()
    {
        var guard = Guard();
        guard.Setup(g => g.TryBegin(UserId, MixEnum.Phoenix, Now, true))
            .Returns(new ImportSlot(ImportSlotOutcome.CoolingDown, TimeSpan.FromMinutes(4)));
        var bus = new Mock<IBus>();

        var result = await Build(bus: bus, guard: guard).Handle(Start(), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.CoolingDown, result.Outcome);
        Assert.Equal(TimeSpan.FromMinutes(4), result.RetryAfter);
        bus.Verify(b => b.Publish(It.IsAny<RunOfficialImportCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ADeepScanNeverWaitsOutTheCooldownButStartsIt()
    {
        // The monthly allowance already rations deep scans (owner, 2026-09-27); it still reads more of
        // piugame than any other run, so the Import button waits after one.
        var guard = Guard();

        var result = await Build(guard: guard).Handle(Start(deepScan: true), CancellationToken.None);

        Assert.Equal(ImportCheckStartOutcome.Started, result.Outcome);
        guard.Verify(g => g.TryBegin(UserId, MixEnum.Phoenix, Now, false), Times.Once);
        guard.Verify(g => g.Started(UserId, MixEnum.Phoenix, Now), Times.Once);
    }

    // ---- builders ----

    private static StartImportCheckCommand Start(bool deepScan = false, bool includeBroken = false)
    {
        return new StartImportCheckCommand(new TypedCredentialSource("user", "pass"), MixEnum.Phoenix,
            "card", "TAG #1", deepScan, includeBroken);
    }

    private static Mock<IImportConcurrencyGuard> Guard(bool userSlot = true)
    {
        var guard = new Mock<IImportConcurrencyGuard>();
        guard.Setup(g => g.TryBegin(It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<bool>()))
            .Returns(userSlot ? ImportSlot.Taken : ImportSlot.AlreadyRunning);
        return guard;
    }

    private static Mock<IMediator> Mediator(int scansLeft = 3, bool spendGranted = true)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetDeepScansRemainingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scansLeft);
        mediator.Setup(m => m.Send(It.IsAny<SpendDeepScanCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(spendGranted);
        return mediator;
    }

    private static StartImportCheckHandler Build(Mock<IBus>? bus = null, Mock<IImportConcurrencyGuard>? guard = null,
        Mock<IMediator>? mediator = null, Mock<IOfficialSiteClient>? site = null)
    {
        if (site == null)
        {
            site = new Mock<IOfficialSiteClient>();
            site.Setup(s => s.SignIn(It.IsAny<MixEnum>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>())).ReturnsAsync("sid");
        }

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.Setup(u => u.User).Returns(new UserBuilder().WithId(UserId).Build());

        return new StartImportCheckHandler(
            (bus ?? new Mock<IBus>()).Object,
            currentUser.Object,
            (guard ?? Guard()).Object,
            (mediator ?? Mediator()).Object,
            site.Object,
            FakeDateTime.At(Now).Object);
    }
}
