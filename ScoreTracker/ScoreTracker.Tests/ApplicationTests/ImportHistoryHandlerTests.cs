using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ImportHistoryHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Started = new(2026, 8, 8, 2, 39, 0, TimeSpan.Zero);

    private static ImportAttemptRecord Attempt(Guid? sessionId = null, ImportOutcome? outcome = null,
        DateTimeOffset? finishedAt = null)
    {
        return new ImportAttemptRecord(Guid.NewGuid(), MixEnum.Phoenix2, ImportKind.Standard, Started,
            finishedAt, outcome, sessionId, null);
    }

    private static ScoreSessionRecord Session(Guid id, int scoreCount)
    {
        return new ScoreSessionRecord(id, UserId, MixEnum.Phoenix2, "officialImport", "TAG #1", "card1",
            Started, Started, scoreCount, scoreCount, 0);
    }

    private static (ImportHistoryHandler Handler, Mock<IMediator> Mediator) Build(
        IReadOnlyList<ImportAttemptRecord> attempts, params ScoreSessionRecord[] sessions)
    {
        var results = new Mock<IImportResultRepository>();
        results.Setup(r => r.GetRecent(UserId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempts);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetScoreSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoreSessionRecord>)sessions);
        return (new ImportHistoryHandler(results.Object, mediator.Object), mediator);
    }

    [Fact]
    public async Task FillsTheScoreCountFromTheSessionTheRunRecorded()
    {
        var sessionId = Guid.NewGuid();
        var (handler, _) = Build(new[] { Attempt(sessionId, ImportOutcome.Completed, Started) },
            Session(sessionId, 37));

        var history = await handler.Handle(new GetImportHistoryQuery(UserId), CancellationToken.None);

        Assert.Equal(37, history.Single().ScoreCount);
    }

    /// <summary>
    ///     A run that died before saving anything has no session. Its count stays null so the page
    ///     can print "—" rather than a confident zero, which would read as "piugame says you have
    ///     no new scores" instead of "this never got far enough to look".
    /// </summary>
    [Fact]
    public async Task ARunWithNoSessionKeepsANullCount()
    {
        var (handler, _) = Build(new[] { Attempt(null, ImportOutcome.PiuGameError, Started) });

        var history = await handler.Handle(new GetImportHistoryQuery(UserId), CancellationToken.None);

        Assert.Null(history.Single().ScoreCount);
    }

    [Fact]
    public async Task ASessionThatNoLongerExistsKeepsANullCount()
    {
        var (handler, _) = Build(new[] { Attempt(Guid.NewGuid(), ImportOutcome.Completed, Started) },
            Session(Guid.NewGuid(), 12));

        var history = await handler.Handle(new GetImportHistoryQuery(UserId), CancellationToken.None);

        Assert.Null(history.Single().ScoreCount);
    }

    /// <summary>
    ///     The Ledger is only asked when at least one run has a session to match, so a player whose
    ///     history is entirely failures does not pay for a session read that can match nothing.
    /// </summary>
    [Fact]
    public async Task DoesNotAskTheLedgerWhenNoRunRecordedASession()
    {
        var (handler, mediator) = Build(new[] { Attempt(), Attempt() });

        await handler.Handle(new GetImportHistoryQuery(UserId), CancellationToken.None);

        mediator.Verify(m => m.Send(It.IsAny<GetScoreSessionsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AnUnfinishedRunReportsItselfAsSuchRatherThanAsAFailure()
    {
        var (handler, _) = Build(new[] { Attempt(Guid.NewGuid()) });

        var attempt = (await handler.Handle(new GetImportHistoryQuery(UserId), CancellationToken.None)).Single();

        Assert.True(attempt.NeverFinished);
        Assert.Null(attempt.Outcome);
        Assert.Null(attempt.Duration);
    }
}
