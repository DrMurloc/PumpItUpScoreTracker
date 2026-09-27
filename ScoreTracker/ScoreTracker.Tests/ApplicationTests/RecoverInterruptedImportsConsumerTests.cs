using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Events;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Import recovery, read from the runs: which sessions the startup pass and the five-minute tick
///     replay, which runs the startup pass closes, and what neither touches
///     (docs/design/import-restart-recovery.md §0). Whether a replay announces anything is the
///     Ledger's call (SessionRecoverySagaTests); this pins what gets offered to it.
/// </summary>
public sealed class RecoverInterruptedImportsConsumerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 3, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BootedAt = Now - TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private sealed class PassContext
    {
        public readonly Mock<IMediator> Mediator = new();
        public readonly Mock<IImportResultRepository> Results = new();
        public readonly List<string> Calls = new();
        public readonly RecoverInterruptedImportsConsumer Consumer;

        public PassContext()
        {
            Consumer = new RecoverInterruptedImportsConsumer(Mediator.Object, Results.Object,
                FakeDateTime.At(Now).Object, NullLogger<RecoverInterruptedImportsConsumer>.Instance);
            Mediator.Setup(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<int>, CancellationToken>((c, _) =>
                    Calls.Add($"replay {((ReplaySessionCommand)c).SessionId}"))
                .ReturnsAsync(1);
            Results.Setup(r => r.MarkInterrupted(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Guid, DateTimeOffset, CancellationToken>((id, _, _) => Calls.Add($"close {id}"))
                .Returns(Task.CompletedTask);
            WithStarted();
            WithFailed();
        }

        public void WithStarted(params ImportRunForRecovery[] runs)
        {
            Results.Setup(r => r.GetUnfinishedStartedBetween(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(runs);
        }

        public void WithFailed(params ImportRunForRecovery[] runs)
        {
            Results.Setup(r => r.GetFailedSince(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(runs);
        }

        public Task Boot()
        {
            var ctx = new Mock<ConsumeContext<RecoverInterruptedImportsCommand>>();
            ctx.SetupGet(c => c.Message).Returns(new RecoverInterruptedImportsCommand(BootedAt));
            ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
            return Consumer.Consume(ctx.Object);
        }

        public Task Tick()
        {
            var ctx = new Mock<ConsumeContext<OverdueScoreBatchesFlushedEvent>>();
            ctx.SetupGet(c => c.Message).Returns(new OverdueScoreBatchesFlushedEvent(Now));
            ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
            return Consumer.Consume(ctx.Object);
        }

        public void VerifyReplayed(Guid sessionId, Times times)
        {
            Mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == sessionId),
                It.IsAny<CancellationToken>()), times);
        }
    }

    private static ImportRunForRecovery Run(Guid? sessionId, DateTimeOffset? finishedAt,
        DateTimeOffset? startedAt = null)
    {
        return new ImportRunForRecovery(Guid.NewGuid(), Guid.NewGuid(), sessionId,
            startedAt ?? BootedAt - TimeSpan.FromMinutes(1), finishedAt);
    }

    // ---- the startup pass ----

    [Fact]
    public async Task TheStartupPassAsksForTheDayOfRunsBeforeTheBootNotBeforeItRuns()
    {
        // The pass runs minutes after the boot, and an import pressed in between is live.
        var ctx = new PassContext();

        await ctx.Boot();

        ctx.Results.Verify(r => r.GetUnfinishedStartedBetween(BootedAt - Day, BootedAt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ARunThatNeverReportedBackIsReplayedAndThenClosed()
    {
        // Replay first: closing it raises the notice that says its scores are already in.
        var ctx = new PassContext();
        var session = Guid.NewGuid();
        var run = Run(session, finishedAt: null);
        ctx.WithStarted(run);

        await ctx.Boot();

        Assert.Equal(new[] { $"replay {session}", $"close {run.Id}" }, ctx.Calls);
    }

    [Fact]
    public async Task ARunThatDiedBeforeItsSessionOpenedIsClosedWithNothingToReplay()
    {
        var ctx = new PassContext();
        var run = Run(sessionId: null, finishedAt: null);
        ctx.WithStarted(run);

        await ctx.Boot();

        ctx.Mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ctx.Results.Verify(r => r.MarkInterrupted(run.Id, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunsOlderThanADayAreClosedWithoutANotice()
    {
        var ctx = new PassContext();

        await ctx.Boot();

        ctx.Results.Verify(r => r.CloseAbandoned(BootedAt - Day, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EveryRunIsTakenWhateverTheCount()
    {
        // The old pass took the 25 oldest unprocessed sessions before skipping any, so 25 typed
        // entries that never announced walled every interrupted import off for good.
        var ctx = new PassContext();
        var sessions = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToArray();
        ctx.WithStarted(sessions.Select(s => Run(s, finishedAt: null)).ToArray());

        await ctx.Boot();

        foreach (var session in sessions) ctx.VerifyReplayed(session, Times.Once());
    }

    [Fact]
    public async Task OneRunsFailureCostsNoOtherRunItsRecovery()
    {
        var ctx = new PassContext();
        var broken = Run(Guid.NewGuid(), finishedAt: null);
        var healthy = Run(Guid.NewGuid(), finishedAt: null);
        ctx.WithStarted(broken, healthy);
        ctx.Mediator.Setup(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == broken.SessionId),
            It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("sql"));

        await ctx.Boot();

        ctx.VerifyReplayed(healthy.SessionId!.Value, Times.Once());
        ctx.Results.Verify(r => r.MarkInterrupted(healthy.Id, Now, It.IsAny<CancellationToken>()), Times.Once);
        // Left open, so the notice never promises scores whose derived work did not run.
        ctx.Results.Verify(r => r.MarkInterrupted(broken.Id, It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- the five-minute tick ----

    [Fact]
    public async Task TheTickReplaysEveryRunThatFailedInTheLastDay()
    {
        var ctx = new PassContext();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        ctx.WithFailed(Run(a, Now - TimeSpan.FromHours(3)), Run(b, Now - TimeSpan.FromMinutes(4)));

        await ctx.Tick();

        ctx.Results.Verify(r => r.GetFailedSince(Now - Day, It.IsAny<CancellationToken>()), Times.Once);
        ctx.VerifyReplayed(a, Times.Once());
        ctx.VerifyReplayed(b, Times.Once());
    }

    [Fact]
    public async Task TheTickNeverClosesARun()
    {
        // A run with no ending mid-life is still working, or belongs to the startup pass.
        var ctx = new PassContext();
        ctx.WithFailed(Run(Guid.NewGuid(), Now - TimeSpan.FromHours(1)));

        await ctx.Tick();

        ctx.Results.Verify(r => r.GetUnfinishedStartedBetween(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Results.Verify(r => r.MarkInterrupted(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Results.Verify(r => r.CloseAbandoned(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheTickKeepsGoingPastAReplayThatThrows()
    {
        var ctx = new PassContext();
        var broken = Guid.NewGuid();
        var healthy = Guid.NewGuid();
        ctx.WithFailed(Run(broken, Now - TimeSpan.FromHours(1)), Run(healthy, Now - TimeSpan.FromHours(2)));
        ctx.Mediator.Setup(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == broken),
            It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("sql"));

        await ctx.Tick();

        ctx.VerifyReplayed(healthy, Times.Once());
    }
}
