using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Restart recovery: rebuilding a session's lost announcement, and the marker that decides
///     whether it needs rebuilding at all (docs/design/import-restart-recovery.md).
/// </summary>
public sealed class SessionRecoverySagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    private sealed class SagaContext
    {
        public readonly Mock<IBus> Bus = new();
        public readonly Mock<IScoreJournalRepository> Journal = new();
        public readonly Mock<IPhoenixRecordRepository> Records = new();
        public readonly Mock<IScoreSessionRepository> Sessions = new();
        public readonly SessionRecoverySaga Saga;

        public SagaContext()
        {
            Saga = new SessionRecoverySaga(Sessions.Object, Journal.Object, Records.Object, Bus.Object,
                FakeDateTime.At(Now).Object, NullLogger<SessionRecoverySaga>.Instance);
        }

        public void WithSession(DateTimeOffset? processedAt = null, MixEnum mix = MixEnum.Phoenix)
        {
            Sessions.Setup(s => s.Get(SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ScoreSessionRecord(SessionId, UserId, mix,
                    ScoreJournalEntry.OfficialImportSource, null, null, Now.AddMinutes(-30),
                    Now.AddMinutes(-10), 0, 0, 0, processedAt));
        }

        public void WithJournal(ScoreJournalEntry[] entries, ScoreJournalEntry[]? histories = null)
        {
            Journal.Setup(j => j.GetSessionEntries(UserId, SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entries);
            Journal.Setup(j => j.GetChartHistories(UserId, It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(histories ?? entries);
        }

        public void WithRecords(params RecordedPhoenixScore[] records)
        {
            Records.Setup(r => r.GetRecordedScores(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(records);
        }
    }

    private static ScoreJournalEntry Row(Guid chartId, int score, DateTimeOffset at, bool isBroken = false)
    {
        return new ScoreJournalEntry(at, ScoreJournalEntry.OfficialImportSource, UserId, chartId,
            PhoenixScore.From(score), null, isBroken, MixEnum.Phoenix, SessionId);
    }

    [Fact]
    public async Task ReplayingAnUnprocessedSessionRepublishesItsBatch()
    {
        var ctx = new SagaContext();
        var chart = Guid.NewGuid();
        ctx.WithSession();
        ctx.WithJournal(new[] { Row(chart, 920_000, Now.AddMinutes(-20)) });
        ctx.WithRecords(new RecordedPhoenixScore(chart, PhoenixScore.From(920_000), PhoenixPlate.MarvelousGame,
            false, Now.AddMinutes(-20)));

        var count = await ctx.Saga.Handle(new ReplaySessionCommand(UserId, SessionId), CancellationToken.None);

        Assert.Equal(1, count);
        ctx.Bus.Verify(b => b.Publish(It.Is<PlayerScoresUpdatedEvent>(e =>
                e.UserId == UserId
                && e.SessionId == SessionId
                && e.Changes.Count == 1
                && e.Changes.Single().ChartId == chart
                && e.Changes.Single().IsNewPass),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     The pass that sends this reads candidates from one vertical and gates them in another,
    ///     so a live drain can land in between. The handler is the last line of defence against
    ///     announcing a session twice.
    /// </summary>
    [Fact]
    public async Task AnAlreadyProcessedSessionIsNotReplayed()
    {
        var ctx = new SagaContext();
        ctx.WithSession(Now.AddMinutes(-5));

        var count = await ctx.Saga.Handle(new ReplaySessionCommand(UserId, SessionId), CancellationToken.None);

        Assert.Equal(0, count);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AMissingSessionIsNotReplayed()
    {
        var ctx = new SagaContext();
        ctx.Sessions.Setup(s => s.Get(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScoreSessionRecord?)null);

        var count = await ctx.Saga.Handle(new ReplaySessionCommand(UserId, SessionId), CancellationToken.None);

        Assert.Equal(0, count);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     A session with nothing announceable in it — all plate-only changes, say — must still be
    ///     marked, or it stays a candidate on every boot from here on.
    /// </summary>
    [Fact]
    public async Task ASessionWithNothingToAnnounceIsMarkedProcessedAnyway()
    {
        var ctx = new SagaContext();
        ctx.WithSession();
        ctx.WithJournal(Array.Empty<ScoreJournalEntry>());

        var count = await ctx.Saga.Handle(new ReplaySessionCommand(UserId, SessionId), CancellationToken.None);

        Assert.Equal(0, count);
        ctx.Sessions.Verify(s => s.MarkProcessed(SessionId, Now, It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Counts are SET, not added. Touch already ran for any batch that drained before the
    ///     interruption, and the replay recomputes the session's whole totals from the journal.
    /// </summary>
    [Fact]
    public async Task ReplaySetsSessionCountsRatherThanAddingToThem()
    {
        var ctx = new SagaContext();
        var newChart = Guid.NewGuid();
        var upscoredChart = Guid.NewGuid();
        ctx.WithSession();
        ctx.WithJournal(
            new[] { Row(newChart, 900_000, Now.AddMinutes(-20)), Row(upscoredChart, 960_000, Now.AddMinutes(-19)) },
            new[]
            {
                Row(newChart, 900_000, Now.AddMinutes(-20)),
                Row(upscoredChart, 930_000, Now.AddDays(-5)),
                Row(upscoredChart, 960_000, Now.AddMinutes(-19))
            });
        ctx.WithRecords(
            new RecordedPhoenixScore(newChart, PhoenixScore.From(900_000), null, false, Now),
            new RecordedPhoenixScore(upscoredChart, PhoenixScore.From(960_000), null, false, Now));

        await ctx.Saga.Handle(new ReplaySessionCommand(UserId, SessionId), CancellationToken.None);

        ctx.Sessions.Verify(s => s.SetCounts(SessionId, Now, 1, 1, It.IsAny<CancellationToken>()), Times.Once);
        ctx.Sessions.Verify(s => s.Touch(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     The marker belongs to the capture chain, not to the publish. Stamping it here would
    ///     mark a session processed whose derived work is still in flight — and lose it for good
    ///     if this process dies again before capture finishes.
    /// </summary>
    [Fact]
    public async Task ReplayDoesNotStampTheMarkerItself()
    {
        var ctx = new SagaContext();
        var chart = Guid.NewGuid();
        ctx.WithSession();
        ctx.WithJournal(new[] { Row(chart, 920_000, Now.AddMinutes(-20)) });
        ctx.WithRecords(new RecordedPhoenixScore(chart, PhoenixScore.From(920_000), null, false, Now));

        await ctx.Saga.Handle(new ReplaySessionCommand(UserId, SessionId), CancellationToken.None);

        ctx.Sessions.Verify(s => s.MarkProcessed(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CapturingHighlightsStampsTheSessionProcessed()
    {
        var ctx = new SagaContext();
        var context = new Mock<ConsumeContext<ScoreHighlightsCapturedEvent>>();
        context.SetupGet(c => c.Message).Returns(ScoreHighlightsCapturedEvent.Create(Now, UserId,
            MixEnum.Phoenix, SessionId, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
            Array.Empty<PlayerMilestoneRecord>()));

        await ctx.Saga.Consume(context.Object);

        ctx.Sessions.Verify(s => s.MarkProcessed(SessionId, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Weekly-placement captures carry no session, so there is nothing to stamp — and stamping
    ///     the wrong thing would silently retire a real recovery candidate.
    /// </summary>
    [Fact]
    public async Task ACaptureWithNoSessionStampsNothing()
    {
        var ctx = new SagaContext();
        var context = new Mock<ConsumeContext<ScoreHighlightsCapturedEvent>>();
        context.SetupGet(c => c.Message).Returns(ScoreHighlightsCapturedEvent.Create(Now, UserId,
            MixEnum.Phoenix, null, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
            Array.Empty<PlayerMilestoneRecord>()));

        await ctx.Saga.Consume(context.Object);

        ctx.Sessions.Verify(s => s.MarkProcessed(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
