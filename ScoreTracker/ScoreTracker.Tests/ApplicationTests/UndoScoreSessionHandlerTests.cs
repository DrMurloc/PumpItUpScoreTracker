using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class UndoScoreSessionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AfterFloor = new(2026, 8, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ChartA = Guid.NewGuid();
    private static readonly Guid ChartB = Guid.NewGuid();

    private readonly Mock<IBus> _bus = new();
    private readonly Mock<IScoreJournalRepository> _journal = new();
    private readonly Mock<IPhoenixRecordRepository> _records = new();
    private readonly Mock<IScoreSessionRepository> _sessions = new();

    private UndoScoreSessionHandler Build()
    {
        return new UndoScoreSessionHandler(_sessions.Object, _journal.Object, _records.Object,
            FakeDateTime.At(Now).Object, _bus.Object);
    }

    private static ScoreSessionRecord Session(DateTimeOffset startedAt, Guid? owner = null)
    {
        return Session(startedAt, MixEnum.Phoenix, owner);
    }

    private static ScoreSessionRecord Session(DateTimeOffset startedAt, MixEnum mix, Guid? owner = null)
    {
        return new ScoreSessionRecord(SessionId, owner ?? UserId, mix,
            ScoreJournalEntry.OfficialImportSource, "SHIRONEKO", "2", startedAt, startedAt, 2, 1, 1);
    }

    private static ScoreJournalEntry Play(Guid chartId, int score, Guid? sessionId, int minute)
    {
        return PlayIn(MixEnum.Phoenix, chartId, score, sessionId, minute);
    }

    private static ScoreJournalEntry PlayIn(MixEnum mix, Guid chartId, int score, Guid? sessionId, int minute)
    {
        return new ScoreJournalEntry(AfterFloor.AddMinutes(minute), ScoreJournalEntry.OfficialImportSource,
            UserId, chartId, (PhoenixScore)score, null, false, mix, sessionId);
    }

    private void GivenSession(ScoreSessionRecord session)
    {
        _sessions.Setup(s => s.Get(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
    }

    private void GivenPlays(IReadOnlyList<ScoreJournalEntry> inSession, IReadOnlyList<ScoreJournalEntry> survivors)
    {
        _journal.Setup(j => j.GetSessionEntries(UserId, SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inSession);
        _journal.Setup(j => j.GetChartHistories(UserId, It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(survivors);
    }

    [Fact]
    public async Task AChartWithAnEarlierPlayIsRestoredToIt()
    {
        GivenSession(Session(AfterFloor));
        GivenPlays(new[] { Play(ChartA, 950_000, SessionId, 10) },
            new[] { Play(ChartA, 910_000, null, 1) });

        var result = await Build().Handle(new UndoScoreSessionCommand(UserId, SessionId), CancellationToken.None);

        Assert.Equal(ScoreSessionUndoOutcome.Undone, result.Outcome);
        Assert.Equal(1, result.ChartsRestored);
        Assert.Equal(0, result.ChartsRemoved);
        _records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix, UserId,
            It.Is<RecordedPhoenixScore>(s => s.ChartId == ChartA && s.Score == (PhoenixScore)910_000),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheOtherMixesScoreIsNeverReplayedIntoThisMixsRecord()
    {
        // A returning song carries ONE ChartId across Phoenix and Phoenix 2, so the chart's
        // history spans both mixes. Undoing a Phoenix 2 session must not put the player's
        // Phoenix 1 score in as their Phoenix 2 best — and nothing would correct it afterwards,
        // because acquisition may only RAISE a record, so the wrong higher number sticks and
        // every re-imported play then reads as "Played".
        GivenSession(Session(AfterFloor, MixEnum.Phoenix2));
        GivenPlays(new[] { PlayIn(MixEnum.Phoenix2, ChartA, 855_000, SessionId, 10) },
            new[] { PlayIn(MixEnum.Phoenix, ChartA, 995_000, null, 1) });

        var result = await Build().Handle(new UndoScoreSessionCommand(UserId, SessionId), CancellationToken.None);

        // Nothing survives in Phoenix 2, so the chart returns to never having been played there.
        Assert.Equal(0, result.ChartsRestored);
        Assert.Equal(1, result.ChartsRemoved);
        _records.Verify(r => r.DeleteRecord(MixEnum.Phoenix2, UserId, ChartA, It.IsAny<CancellationToken>()),
            Times.Once);
        _records.Verify(r => r.UpdateBestAttempt(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<RecordedPhoenixScore>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AChartTheSessionWasFirstToTouchIsRemovedEntirely()
    {
        // Nothing came before, so there is nothing to put back — the chart returns to never
        // having been played rather than keeping the import's score.
        GivenSession(Session(AfterFloor));
        GivenPlays(new[] { Play(ChartB, 912_000, SessionId, 10) }, Array.Empty<ScoreJournalEntry>());

        var result = await Build().Handle(new UndoScoreSessionCommand(UserId, SessionId), CancellationToken.None);

        Assert.Equal(0, result.ChartsRestored);
        Assert.Equal(1, result.ChartsRemoved);
        _records.Verify(r => r.DeleteRecord(MixEnum.Phoenix, UserId, ChartB, It.IsAny<CancellationToken>()),
            Times.Once);
        _records.Verify(r => r.UpdateBestAttempt(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<RecordedPhoenixScore>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheSessionRowAndItsProgressionRecordsGoToo()
    {
        GivenSession(Session(AfterFloor));
        GivenPlays(new[] { Play(ChartA, 950_000, SessionId, 10) },
            new[] { Play(ChartA, 910_000, null, 1) });

        await Build().Handle(new UndoScoreSessionCommand(UserId, SessionId), CancellationToken.None);

        _journal.Verify(j => j.DeleteSession(UserId, SessionId, It.IsAny<CancellationToken>()), Times.Once);
        _sessions.Verify(s => s.Delete(SessionId, It.IsAny<CancellationToken>()), Times.Once);
        // Highlights and milestones are PlayerProgress's and are not recomputed, so they have to
        // be told rather than left to fall out.
        _bus.Verify(b => b.Publish(It.Is<ScoreSessionUndoneEvent>(e =>
            e.UserId == UserId && e.SessionId == SessionId && e.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
        // And stats/Pumbility/titles recompute through the pipeline that already exists.
        _bus.Verify(b => b.Publish(It.Is<PlayerScoresUpdatedEvent>(e => e.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ASessionBelongingToSomebodyElseIsNotFound()
    {
        // The session id is a bare Guid on a public contract, so ownership is checked here
        // rather than trusted from the caller.
        GivenSession(Session(AfterFloor, Guid.NewGuid()));

        var result = await Build().Handle(new UndoScoreSessionCommand(UserId, SessionId), CancellationToken.None);

        Assert.Equal(ScoreSessionUndoOutcome.NotFound, result.Outcome);
        _journal.Verify(j => j.DeleteSession(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AMissingSessionIsNotFound()
    {
        _sessions.Setup(s => s.Get(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScoreSessionRecord?)null);

        var result = await Build().Handle(new UndoScoreSessionCommand(UserId, SessionId), CancellationToken.None);

        Assert.Equal(ScoreSessionUndoOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task ASessionOlderThanTheFloorRefusesRatherThanGuessing()
    {
        // Before the floor we did not record when scores arrived, only when they were played,
        // so there is no honest way to undo one.
        GivenSession(Session(ScoreSessionRecord.UndoFloor.AddDays(-1)));

        var result = await Build().Handle(new UndoScoreSessionCommand(UserId, SessionId), CancellationToken.None);

        Assert.Equal(ScoreSessionUndoOutcome.TooOld, result.Outcome);
        _journal.Verify(j => j.DeleteSession(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _sessions.Verify(s => s.Delete(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PlaysFromOtherSessionsSurviveAndCanWin()
    {
        // The independence guarantee at handler level: a chart a later session also improved
        // keeps the later score, because that play is still there to be replayed.
        GivenSession(Session(AfterFloor));
        GivenPlays(new[] { Play(ChartA, 950_000, SessionId, 10) },
            new[] { Play(ChartA, 910_000, null, 1), Play(ChartA, 985_000, Guid.NewGuid(), 40) });

        await Build().Handle(new UndoScoreSessionCommand(UserId, SessionId), CancellationToken.None);

        _records.Verify(r => r.UpdateBestAttempt(MixEnum.Phoenix, UserId,
            It.Is<RecordedPhoenixScore>(s => s.Score == (PhoenixScore)985_000),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
