using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Events;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The startup recovery pass: which interrupted sessions get replayed, which runs get closed,
///     and — just as important — what it leaves alone
///     (docs/design/import-restart-recovery.md §4).
/// </summary>
public sealed class RecoverInterruptedImportsConsumerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    // The pass runs at boot; anything that began before that instant belonged to the process
    // that just died.
    private static readonly DateTimeOffset BootedAt = Now;
    // Comfortably past ScoreBatchPolicy.StaleAfter, so the sweep's gate is unambiguous rather
    // than sitting on its boundary.
    private static readonly TimeSpan LongAgo = TimeSpan.FromMinutes(45);

    private sealed class PassContext
    {
        public readonly Mock<IMediator> Mediator = new();
        public readonly Mock<IImportResultRepository> Results = new();
        public readonly RecoverInterruptedImportsConsumer Consumer;

        public PassContext()
        {
            Consumer = new RecoverInterruptedImportsConsumer(Mediator.Object, Results.Object,
                FakeDateTime.At(Now).Object, NullLogger<RecoverInterruptedImportsConsumer>.Instance);
            Mediator.Setup(m => m.Send(It.IsAny<GetUnprocessedSessionsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<ScoreSessionRecord>)Array.Empty<ScoreSessionRecord>());
            Results.Setup(r => r.GetForSessions(It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<ImportRunForSession>());
            Mediator.Setup(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
        }

        public void WithUnprocessed(params Guid[] sessionIds)
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetUnprocessedSessionsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<ScoreSessionRecord>)sessionIds
                    .Select(id => new ScoreSessionRecord(id, UserId, MixEnum.Phoenix,
                        ScoreJournalEntry.OfficialImportSource, null, null, Now - LongAgo, Now - LongAgo,
                        0, 0, 0))
                    .ToArray());
        }

        public void WithRuns(params ImportRunForSession[] runs)
        {
            Results.Setup(r => r.GetForSessions(It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(runs);
        }

        public Task Run()
        {
            var ctx = new Mock<ConsumeContext<RecoverInterruptedImportsCommand>>();
            ctx.SetupGet(c => c.Message).Returns(new RecoverInterruptedImportsCommand(BootedAt));
            ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
            return Consumer.Consume(ctx.Object);
        }

        public Task Sweep()
        {
            var ctx = new Mock<ConsumeContext<OverdueScoreBatchesFlushedEvent>>();
            ctx.SetupGet(c => c.Message).Returns(new OverdueScoreBatchesFlushedEvent(Now));
            ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
            return Consumer.Consume(ctx.Object);
        }

        /// <summary>Unprocessed sessions, each a minute older than the one before it.</summary>
        public void WithUnprocessedAged(params Guid[] sessionIds)
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetUnprocessedSessionsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<ScoreSessionRecord>)sessionIds
                    .Select((id, i) => new ScoreSessionRecord(id, UserId, MixEnum.Phoenix,
                        ScoreJournalEntry.OfficialImportSource, null, null,
                        Now - LongAgo + TimeSpan.FromMinutes(i), Now - LongAgo, 0, 0, 0))
                    .ToArray());
        }

        /// <summary>Unprocessed sessions carrying counts, i.e. a batch that already announced.</summary>
        public void WithDrainedSessions(params Guid[] sessionIds)
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetUnprocessedSessionsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<ScoreSessionRecord>)sessionIds
                    .Select(id => new ScoreSessionRecord(id, UserId, MixEnum.Phoenix,
                        ScoreJournalEntry.OfficialImportSource, null, null, Now - LongAgo, Now - LongAgo,
                        4, 3, 1))
                    .ToArray());
        }
    }

    /// <summary>
    ///     The mid-life sweep replays a run whose batch has had its whole window to drain and did
    ///     not — the shape of a lost drain schedule inside a process that never restarted.
    /// </summary>
    [Fact]
    public async Task TheSweepReplaysARunWhoseDrainWindowHasPassed()
    {
        var ctx = new PassContext();
        var session = Guid.NewGuid();
        ctx.WithUnprocessed(session);
        ctx.WithRuns(new ImportRunForSession(Guid.NewGuid(), session, Now - LongAgo, Now - LongAgo));

        await ctx.Sweep();

        ctx.Mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == session),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     ⚠ The mirror of the boot pass's guard. Mid-life every run began after the boot, so the
    ///     boot test would skip all of them — but staleness cuts the other way too: a run that
    ///     finished a minute ago still has a live batch counting down, and replaying it would
    ///     announce the same scores the drain is about to.
    /// </summary>
    [Fact]
    public async Task TheSweepLeavesARunWhoseBatchIsStillCountingDown()
    {
        var ctx = new PassContext();
        var session = Guid.NewGuid();
        var justFinished = Now - TimeSpan.FromMinutes(1);
        ctx.WithUnprocessed(session);
        ctx.WithRuns(new ImportRunForSession(Guid.NewGuid(), session, Now - LongAgo, justFinished));

        await ctx.Sweep();

        ctx.Mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     ⚠ ProcessedAt is an END marker, so on its own it cannot tell "the batch never drained"
    ///     from "the batch drained and capture is still working". A first full-account import can
    ///     spend minutes in capture; replaying underneath it publishes the same scores twice and
    ///     posts a second card. The counts are the discriminator — Touch writes them when a batch
    ///     announces, long before ProcessedAt lands.
    /// </summary>
    [Fact]
    public async Task TheSweepLeavesASessionWhoseBatchAlreadyAnnounced()
    {
        var ctx = new PassContext();
        var session = Guid.NewGuid();
        ctx.WithDrainedSessions(session);
        ctx.WithRuns(new ImportRunForSession(Guid.NewGuid(), session, Now - LongAgo, Now - LongAgo));

        await ctx.Sweep();

        ctx.Mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Every replay drops a full capture chain on the bus, and the failure this recovers from
    ///     strands sessions site-wide — so a pass sheds the newest rather than herding the whole
    ///     backlog through one tick.
    /// </summary>
    [Fact]
    public async Task TheSweepCapsOnePassAndTakesTheOldestFirst()
    {
        var ctx = new PassContext();
        var sessions = Enumerable.Range(0, 30).Select(_ => Guid.NewGuid()).ToArray();
        ctx.WithUnprocessedAged(sessions);
        ctx.WithRuns(sessions.Select(s => new ImportRunForSession(Guid.NewGuid(), s, Now - LongAgo,
            Now - LongAgo)).ToArray());

        await ctx.Sweep();

        ctx.Mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(25));
        // Oldest first, so the newest five are what gets deferred to the next pass.
        foreach (var deferred in sessions.TakeLast(5))
            ctx.Mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == deferred),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     A run with no ending is either still scraping — a deep scan outlives this window with no
    ///     batch open yet — or died with its process, which is the boot pass's candidate. Either
    ///     way the sweep must not close it out from under a player mid-import.
    /// </summary>
    [Fact]
    public async Task TheSweepNeitherClosesNorReplaysARunThatNeverReportedAnEnding()
    {
        var ctx = new PassContext();
        var session = Guid.NewGuid();
        ctx.WithUnprocessed(session);
        ctx.WithRuns(new ImportRunForSession(Guid.NewGuid(), session, Now - LongAgo, null));

        await ctx.Sweep();

        ctx.Results.Verify(r => r.MarkInterrupted(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ARunThatFinishedLongAgoWithUnprocessedWorkIsReplayed()
    {
        var ctx = new PassContext();
        var session = Guid.NewGuid();
        ctx.WithUnprocessed(session);
        ctx.WithRuns(new ImportRunForSession(Guid.NewGuid(), session, Now - LongAgo, Now - LongAgo));

        await ctx.Run();

        ctx.Mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == session),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     ⚠ The regression that shipped. A run the restart itself killed is, at the moment the
    ///     pass runs, SECONDS old — so an age-based guard ("younger than the batch hold window,
    ///     leave it alone") skipped precisely the runs this feature exists for, and nothing looked
    ///     again until the next restart. Observed in the field 2026-08-10: three interrupted runs
    ///     sitting at a null outcome with the dialog never firing.
    ///     <para>
    ///         Age is the wrong test. At boot the accumulator is empty, so nothing from the
    ///         previous process can drain however recently it ran.
    ///     </para>
    /// </summary>
    [Fact]
    public async Task ARunTheRestartItselfKilledSecondsAgoIsStillRecovered()
    {
        var ctx = new PassContext();
        var session = Guid.NewGuid();
        var runId = Guid.NewGuid();
        ctx.WithUnprocessed(session);
        ctx.WithRuns(new ImportRunForSession(runId, session, BootedAt.AddSeconds(-10), null));

        await ctx.Run();

        ctx.Results.Verify(r => r.MarkInterrupted(runId, Now, It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == session),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     A run that began AFTER this boot is live: its batch is in memory with a real deadline,
    ///     and replaying it would announce a session about to announce itself. A future restart
    ///     recovers it if one interrupts it.
    /// </summary>
    [Fact]
    public async Task ARunThatStartedAfterThisBootIsLeftAlone()
    {
        var ctx = new PassContext();
        var session = Guid.NewGuid();
        ctx.WithUnprocessed(session);
        ctx.WithRuns(new ImportRunForSession(Guid.NewGuid(), session, BootedAt.AddSeconds(5), null));

        await ctx.Run();

        ctx.Mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ctx.Results.Verify(r => r.MarkInterrupted(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     Interrupted mid-scrape: closed AND replayed. The replay matters more than it looks —
    ///     re-importing will not recover the derived work for the scores it already saved, because
    ///     the records match and those charts never re-enter a batch.
    /// </summary>
    [Fact]
    public async Task ARunThatNeverFinishedIsClosedAndStillReplayed()
    {
        var ctx = new PassContext();
        var session = Guid.NewGuid();
        var runId = Guid.NewGuid();
        ctx.WithUnprocessed(session);
        ctx.WithRuns(new ImportRunForSession(runId, session, Now - LongAgo, null));

        await ctx.Run();

        ctx.Results.Verify(r => r.MarkInterrupted(runId, Now, It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == session),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Manual entry, CSV upload and API submissions never mint an ImportResult, so nothing here
    ///     can say whether their batch has had its chance. Deliberately out of scope (§3).
    /// </summary>
    [Fact]
    public async Task ASessionWithNoImportRunBehindItIsSkipped()
    {
        var ctx = new PassContext();
        ctx.WithUnprocessed(Guid.NewGuid());
        ctx.WithRuns();

        await ctx.Run();

        ctx.Mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NothingUnprocessedMeansNoWorkAtAll()
    {
        var ctx = new PassContext();

        await ctx.Run();

        ctx.Results.Verify(r => r.GetForSessions(It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EverySessionInTheBacklogIsHandled()
    {
        var ctx = new PassContext();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        ctx.WithUnprocessed(a, b);
        ctx.WithRuns(
            new ImportRunForSession(Guid.NewGuid(), a, Now - LongAgo, Now - LongAgo),
            new ImportRunForSession(Guid.NewGuid(), b, Now - LongAgo, Now - LongAgo));

        await ctx.Run();

        ctx.Mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == a),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == b),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
