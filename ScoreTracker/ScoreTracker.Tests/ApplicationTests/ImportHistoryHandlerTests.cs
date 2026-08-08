using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class ImportHistoryHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Started = new(2026, 8, 8, 2, 39, 0, TimeSpan.Zero);

    private static ImportAttemptRecord Attempt(ImportOutcome? outcome = ImportOutcome.Completed,
        DateTimeOffset? finishedAt = null, int? scoreCount = null, Guid? sessionId = null)
    {
        return new ImportAttemptRecord(Guid.NewGuid(), MixEnum.Phoenix2, ImportKind.Standard, Started,
            finishedAt, outcome, sessionId, scoreCount);
    }

    private static ImportHistoryHandler Build(params ImportAttemptRecord[] attempts)
    {
        var results = new Mock<IImportResultRepository>();
        results.Setup(r => r.GetRecent(UserId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ImportAttemptRecord>)attempts);
        return new ImportHistoryHandler(results.Object);
    }

    /// <summary>
    ///     The count is stamped on the run when it closes, not read off the Ledger's session. That
    ///     counter is written when the score batch DRAINS — a ~2 minute in-memory debounce — so an
    ///     early look or an app restart inside the window leaves it at zero permanently while the
    ///     journal holds the rows. Field-observed 2026-08-08: a check that saved seven scores sat
    ///     at ScoreCount 0 with seven journal rows behind it.
    /// </summary>
    [Fact]
    public async Task ReportsTheCountTheRunRecordedForItself()
    {
        var handler = Build(Attempt(finishedAt: Started.AddMinutes(2), scoreCount: 6));

        var history = await handler.Handle(new GetImportHistoryQuery(UserId), CancellationToken.None);

        Assert.Equal(6, history.Single().ScoreCount);
    }

    /// <summary>
    ///     Zero is a real answer now that the run counts itself — importing twice in a row
    ///     legitimately saves nothing, and saying so beats an em dash that reads as "unknown".
    /// </summary>
    [Fact]
    public async Task AnImportThatSavedNothingSaysZeroRatherThanUnknown()
    {
        var handler = Build(Attempt(finishedAt: Started.AddMinutes(2), scoreCount: 0));

        var history = await handler.Handle(new GetImportHistoryQuery(UserId), CancellationToken.None);

        Assert.Equal(0, history.Single().ScoreCount);
    }

    [Fact]
    public async Task ARunThatNeverReportedBackHasNoCountAtAll()
    {
        var handler = Build(Attempt(null));

        var attempt = (await handler.Handle(new GetImportHistoryQuery(UserId), CancellationToken.None)).Single();

        Assert.Null(attempt.ScoreCount);
        Assert.True(attempt.NeverFinished);
        Assert.Null(attempt.Duration);
    }

    [Fact]
    public async Task AFailedRunCarriesNoCount()
    {
        var handler = Build(Attempt(ImportOutcome.PiuGameError, Started.AddMinutes(2)));

        Assert.Null((await handler.Handle(new GetImportHistoryQuery(UserId), CancellationToken.None))
            .Single().ScoreCount);
    }

    [Fact]
    public async Task PassesTheRequestedTakeThrough()
    {
        var results = new Mock<IImportResultRepository>();
        results.Setup(r => r.GetRecent(UserId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ImportAttemptRecord>)new[] { Attempt() });

        await new ImportHistoryHandler(results.Object)
            .Handle(new GetImportHistoryQuery(UserId, 3), CancellationToken.None);

        results.Verify(r => r.GetRecent(UserId, 3, It.IsAny<CancellationToken>()), Times.Once);
    }
}
