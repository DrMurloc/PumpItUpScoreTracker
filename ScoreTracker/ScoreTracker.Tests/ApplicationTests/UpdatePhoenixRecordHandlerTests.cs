using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class UpdatePhoenixRecordHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChartId = Guid.NewGuid();

    [Fact]
    public async Task NewClearSavesRecordSchedulesFireAndAddsBatch()
    {
        var ctx = new HandlerContext();
        ctx.Batches.Setup(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, true,
            It.IsAny<PhoenixScore?>())).Returns(true);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Records.Verify(r => r.UpdateBestAttempt(UserId,
            It.Is<RecordedPhoenixScore>(s => s.ChartId == ChartId && !s.IsBroken
                                             && s.Score == (PhoenixScore)950000
                                             && s.Plate == PhoenixPlate.SuperbGame
                                             && s.RecordedDate == Now),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Scheduler.Verify(s => s.SchedulePublish(
            It.IsAny<DateTime>(),
            It.Is<UpdatePhoenixRecordHandler.TryFireScoreCommand>(m => m.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Batches.Verify(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, true,
            It.Is<PhoenixScore?>(s => !s.HasValue)), Times.Once);
    }

    [Fact]
    public async Task KeepBestStatsKeepsHigherExistingScore()
    {
        var ctx = new HandlerContext();
        ctx.GivenExistingScore(score: 950000, plate: PhoenixPlate.SuperbGame, isBroken: false);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 900000,
                Plate: PhoenixPlate.SuperbGame, KeepBestStats: true),
            CancellationToken.None);

        ctx.Records.Verify(r => r.UpdateBestAttempt(UserId,
            It.Is<RecordedPhoenixScore>(s => s.Score == (PhoenixScore)950000),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KeepBestStatsKeepsHigherExistingPlate()
    {
        var ctx = new HandlerContext();
        ctx.GivenExistingScore(score: 900000, plate: PhoenixPlate.PerfectGame, isBroken: false);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 900000,
                Plate: PhoenixPlate.FairGame, KeepBestStats: true),
            CancellationToken.None);

        ctx.Records.Verify(r => r.UpdateBestAttempt(UserId,
            It.Is<RecordedPhoenixScore>(s => s.Plate == PhoenixPlate.PerfectGame),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KeepBestStatsPreservesExistingClearWhenRequestIsBroken()
    {
        var ctx = new HandlerContext();
        ctx.GivenExistingScore(score: 900000, plate: PhoenixPlate.SuperbGame, isBroken: false);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: true, Score: 950000,
                Plate: PhoenixPlate.SuperbGame, KeepBestStats: true),
            CancellationToken.None);

        ctx.Records.Verify(r => r.UpdateBestAttempt(UserId,
            It.Is<RecordedPhoenixScore>(s => !s.IsBroken),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithoutKeepBestStatsOverwritesWithRequestValues()
    {
        var ctx = new HandlerContext();
        ctx.GivenExistingScore(score: 950000, plate: PhoenixPlate.PerfectGame, isBroken: false);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 800000,
                Plate: PhoenixPlate.FairGame, KeepBestStats: false),
            CancellationToken.None);

        ctx.Records.Verify(r => r.UpdateBestAttempt(UserId,
            It.Is<RecordedPhoenixScore>(s => s.Score == (PhoenixScore)800000
                                             && s.Plate == PhoenixPlate.FairGame),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NewClearWhenExistingWasBrokenAddsAsNewClearOnly()
    {
        var ctx = new HandlerContext();
        // Existing was broken at the same score → transition to a clear is a new clear,
        // and 950000 < 950000 is false so it is NOT also an upscore.
        ctx.GivenExistingScore(score: 950000, plate: PhoenixPlate.SuperbGame, isBroken: true);
        ctx.Batches.Setup(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, true,
            It.IsAny<PhoenixScore?>())).Returns(true);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Batches.Verify(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, true,
            It.Is<PhoenixScore?>(s => !s.HasValue)), Times.Once);
    }

    [Fact]
    public async Task ClearedWithHigherScoreFromBrokenAddsBothFlags()
    {
        // Broken→clear with a higher score: AddToBatch is called with isNewClear=true AND
        // upscoredFrom=previousScore in a single atomic call. New-clear precedence is
        // resolved inside the accumulator.
        var ctx = new HandlerContext();
        ctx.GivenExistingScore(score: 700000, plate: PhoenixPlate.RoughGame, isBroken: true);
        ctx.Batches.Setup(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, true,
            It.IsAny<PhoenixScore?>())).Returns(true);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Batches.Verify(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, true,
            It.Is<PhoenixScore?>(s => s.HasValue && s.Value == (PhoenixScore)700000)), Times.Once);
    }

    [Fact]
    public async Task UpscoreAddsAsUpscoreOnly()
    {
        var ctx = new HandlerContext();
        ctx.GivenExistingScore(score: 900000, plate: PhoenixPlate.SuperbGame, isBroken: false);
        ctx.Batches.Setup(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, false,
            It.IsAny<PhoenixScore?>())).Returns(true);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 970000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Batches.Verify(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, false,
            It.Is<PhoenixScore?>(s => s.HasValue && s.Value == (PhoenixScore)900000)), Times.Once);
    }

    [Fact]
    public async Task JournalsSubmissionAsReceivedEvenWhenKeepBestStatsDiscardsIt()
    {
        var ctx = new HandlerContext();
        ctx.GivenExistingScore(score: 950000, plate: PhoenixPlate.PerfectGame, isBroken: false);

        // Worse-on-every-axis submission with KeepBestStats: the stored best keeps the
        // existing values and no batch/schedule fires — but the journal still gets the
        // raw submission, because it is play history rather than best-attempt state.
        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: true, Score: 900000,
                Plate: PhoenixPlate.FairGame, KeepBestStats: true),
            CancellationToken.None);

        ctx.Journal.Verify(j => j.Append(
            It.Is<ScoreJournalEntry>(e => e.UserId == UserId
                                          && e.ChartId == ChartId
                                          && e.OccurredAt == Now
                                          && e.Source == ScoreJournalEntry.ManualSource
                                          && e.Score == (PhoenixScore)900000
                                          && e.Plate == PhoenixPlate.FairGame
                                          && e.IsBroken),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Batches.Verify(b => b.AddToBatch(It.IsAny<Guid>(), It.IsAny<DateTime>(),
            It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<PhoenixScore?>()), Times.Never);
    }

    [Fact]
    public async Task JournalsSubmissionWithItsDeclaredSource()
    {
        var ctx = new HandlerContext();

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame, Source: ScoreJournalEntry.OfficialImportSource),
            CancellationToken.None);

        ctx.Journal.Verify(j => j.Append(
            It.Is<ScoreJournalEntry>(e => e.Source == ScoreJournalEntry.OfficialImportSource),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JournalsSubmissionWithItsDeclaredMix()
    {
        var ctx = new HandlerContext();

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame, Mix: MixEnum.Phoenix2),
            CancellationToken.None);

        ctx.Journal.Verify(j => j.Append(
            It.Is<ScoreJournalEntry>(e => e.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task JournalsPhoenixMixWhenCommandDoesNotDeclareOne()
    {
        var ctx = new HandlerContext();

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Journal.Verify(j => j.Append(
            It.Is<ScoreJournalEntry>(e => e.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoNewClearAndNoUpscoreDoesNotScheduleOrBatch()
    {
        var ctx = new HandlerContext();
        // Existing un-broken with same score → not a new clear, not an upscore.
        ctx.GivenExistingScore(score: 900000, plate: PhoenixPlate.SuperbGame, isBroken: false);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 900000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Batches.Verify(b => b.AddToBatch(It.IsAny<Guid>(), It.IsAny<DateTime>(),
            It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<PhoenixScore?>()), Times.Never);
        ctx.Scheduler.Verify(s => s.SchedulePublish(
            It.IsAny<DateTime>(),
            It.IsAny<UpdatePhoenixRecordHandler.TryFireScoreCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SkipsSchedulingWhenBatchAlreadyActiveButStillAdds()
    {
        var ctx = new HandlerContext();
        // Batch already active: AddToBatch returns false, so the handler must NOT
        // schedule a new fire message — but the call itself must still happen.
        ctx.Batches.Setup(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, true,
            It.IsAny<PhoenixScore?>())).Returns(false);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Scheduler.Verify(s => s.SchedulePublish(
            It.IsAny<DateTime>(),
            It.IsAny<UpdatePhoenixRecordHandler.TryFireScoreCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Batches.Verify(b => b.AddToBatch(UserId, It.IsAny<DateTime>(), ChartId, true,
            It.IsAny<PhoenixScore?>()), Times.Once);
    }

    [Fact]
    public async Task ReschedulesAndDoesNotPublishWhenFireAtIsInTheFuture()
    {
        var ctx = new HandlerContext();
        var fireAt = Now.UtcDateTime + TimeSpan.FromMinutes(1);
        ctx.Batches.Setup(b => b.GetFireAt(UserId)).Returns(fireAt);

        await ctx.Handler.Consume(BuildContext(new UpdatePhoenixRecordHandler.TryFireScoreCommand(UserId)));

        // Reschedule must use a small (+5s) buffer, NOT +2min — the latter compounds on
        // every retry and starves active players.
        ctx.Scheduler.Verify(s => s.SchedulePublish(
            fireAt + TimeSpan.FromSeconds(5),
            It.Is<UpdatePhoenixRecordHandler.TryFireScoreCommand>(m => m.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Batches.Verify(b => b.TakeBatch(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TakesBatchAndPublishesWhenFireAtHasBeenReached()
    {
        var ctx = new HandlerContext();
        var fireAt = Now.UtcDateTime - TimeSpan.FromSeconds(1); // already past
        var newCharts = new[] { Guid.NewGuid() };
        var upscored = new Dictionary<Guid, int> { { Guid.NewGuid(), 900000 } };
        ctx.Batches.Setup(b => b.GetFireAt(UserId)).Returns(fireAt);
        ctx.Batches.Setup(b => b.TakeBatch(UserId)).Returns(new PendingScoreBatch(newCharts, upscored));

        await ctx.Handler.Consume(BuildContext(new UpdatePhoenixRecordHandler.TryFireScoreCommand(UserId)));

        ctx.Bus.Verify(b => b.Publish(
            It.Is<PlayerScoresUpdatedEvent>(e => e.UserId == UserId
                                                && e.Changes.Count(c => c.IsNewPass) == newCharts.Length
                                                && e.Changes.Count(c => !c.IsNewPass) == upscored.Count),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Scheduler.Verify(s => s.SchedulePublish(
            It.IsAny<DateTime>(),
            It.IsAny<UpdatePhoenixRecordHandler.TryFireScoreCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DualPublishesFatContractEventCarryingScoreFacts()
    {
        var ctx = new HandlerContext();
        var newChart = Guid.NewGuid();
        var upscoredChart = Guid.NewGuid();
        ctx.Batches.Setup(b => b.GetFireAt(UserId)).Returns(Now.UtcDateTime - TimeSpan.FromSeconds(1));
        ctx.Batches.Setup(b => b.TakeBatch(UserId)).Returns(new PendingScoreBatch(
            new[] { newChart }, new Dictionary<Guid, int> { { upscoredChart, 900000 } }));
        ctx.Records.Setup(r => r.GetRecordedScores(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(newChart, 985000, PhoenixPlate.ExtremeGame, false, Now),
                new RecordedPhoenixScore(upscoredChart, 950000, PhoenixPlate.FairGame, false, Now)
            });

        await ctx.Handler.Consume(BuildContext(new UpdatePhoenixRecordHandler.TryFireScoreCommand(UserId)));

        ctx.Bus.Verify(b => b.Publish(
            It.Is<PlayerScoresUpdatedEvent>(e =>
                e.UserId == UserId
                && e.OccurredAt == Now
                && e.SchemaVersion == PlayerScoresUpdatedEvent.CurrentSchemaVersion
                && e.EventId != Guid.Empty
                && e.Changes.Count == 2
                && e.Changes.Single(c => c.ChartId == newChart).IsNewPass
                && e.Changes.Single(c => c.ChartId == newChart).NewScore == 985000
                && e.Changes.Single(c => c.ChartId == newChart).Plate == "ExtremeGame"
                && !e.Changes.Single(c => c.ChartId == upscoredChart).IsNewPass
                && e.Changes.Single(c => c.ChartId == upscoredChart).OldScore == 900000
                && e.Changes.Single(c => c.ChartId == upscoredChart).NewScore == 950000),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryFireNoOpsWhenBatchAlreadyDrained()
    {
        // GetFireAt returns null = the batch was already drained by a concurrent
        // TryFire or the Hangfire flush. Consumer must not crash, must not publish,
        // must not reschedule.
        var ctx = new HandlerContext();
        ctx.Batches.Setup(b => b.GetFireAt(UserId)).Returns((DateTime?)null);

        await ctx.Handler.Consume(BuildContext(new UpdatePhoenixRecordHandler.TryFireScoreCommand(UserId)));

        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Scheduler.Verify(s => s.SchedulePublish(
            It.IsAny<DateTime>(),
            It.IsAny<UpdatePhoenixRecordHandler.TryFireScoreCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Batches.Verify(b => b.TakeBatch(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TryFireNoOpsWhenAnotherDrainTookTheBatchBetweenGetAndTake()
    {
        // FireAt is past (so we proceed to TakeBatch), but TakeBatch returns null
        // because another consumer drained between our GetFireAt and TakeBatch.
        var ctx = new HandlerContext();
        var fireAt = Now.UtcDateTime - TimeSpan.FromSeconds(1);
        ctx.Batches.Setup(b => b.GetFireAt(UserId)).Returns(fireAt);
        ctx.Batches.Setup(b => b.TakeBatch(UserId)).Returns((PendingScoreBatch?)null);

        await ctx.Handler.Consume(BuildContext(new UpdatePhoenixRecordHandler.TryFireScoreCommand(UserId)));

        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FlushDrainsOverdueBatchesAndPublishesPlayerScoreUpdated()
    {
        var ctx = new HandlerContext();
        var overdueUserA = Guid.NewGuid();
        var overdueUserB = Guid.NewGuid();
        var futureUser = Guid.NewGuid();
        var newChartA = Guid.NewGuid();
        var upscoreChartB = Guid.NewGuid();
        ctx.Batches.Setup(b => b.Dump()).Returns(new[]
        {
            new BatchAccumulatorSnapshotEntry(overdueUserA, Now.UtcDateTime - TimeSpan.FromSeconds(1),
                new[] { newChartA }, new Dictionary<Guid, int>()),
            new BatchAccumulatorSnapshotEntry(overdueUserB, Now.UtcDateTime - TimeSpan.FromMinutes(10),
                Array.Empty<Guid>(), new Dictionary<Guid, int> { { upscoreChartB, 850000 } }),
            new BatchAccumulatorSnapshotEntry(futureUser, Now.UtcDateTime + TimeSpan.FromMinutes(1),
                new[] { Guid.NewGuid() }, new Dictionary<Guid, int>())
        });
        ctx.Batches.Setup(b => b.TakeBatch(overdueUserA))
            .Returns(new PendingScoreBatch(new[] { newChartA }, new Dictionary<Guid, int>()));
        ctx.Batches.Setup(b => b.TakeBatch(overdueUserB))
            .Returns(new PendingScoreBatch(Array.Empty<Guid>(),
                new Dictionary<Guid, int> { { upscoreChartB, 850000 } }));

        await ctx.Handler.Consume(BuildContext(new FlushOverdueScoreBatchesCommand()));

        ctx.Batches.Verify(b => b.TakeBatch(overdueUserA), Times.Once);
        ctx.Batches.Verify(b => b.TakeBatch(overdueUserB), Times.Once);
        ctx.Batches.Verify(b => b.TakeBatch(futureUser), Times.Never);
        ctx.Bus.Verify(b => b.Publish(
            It.Is<PlayerScoresUpdatedEvent>(e => e.UserId == overdueUserA
                                                && e.Changes.Count == 1
                                                && e.Changes.Single(c => c.IsNewPass).ChartId == newChartA),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(
            It.Is<PlayerScoresUpdatedEvent>(e => e.UserId == overdueUserB
                                                && e.Changes.Any(c => !c.IsNewPass && c.ChartId == upscoreChartB)),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(
            It.Is<PlayerScoresUpdatedEvent>(e => e.UserId == futureUser),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FlushSkipsRacedBatchesWhenTakeBatchReturnsNull()
    {
        // If the original TryFireScoreCommand drains the user between Dump() and
        // TakeBatch(), TakeBatch returns null — we must NOT publish a noisy empty event.
        var ctx = new HandlerContext();
        var racedUser = Guid.NewGuid();
        ctx.Batches.Setup(b => b.Dump()).Returns(new[]
        {
            new BatchAccumulatorSnapshotEntry(racedUser, Now.UtcDateTime - TimeSpan.FromSeconds(1),
                new[] { Guid.NewGuid() }, new Dictionary<Guid, int>())
        });
        ctx.Batches.Setup(b => b.TakeBatch(racedUser)).Returns((PendingScoreBatch?)null);

        await ctx.Handler.Consume(BuildContext(new FlushOverdueScoreBatchesCommand()));

        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FlushDoesNothingWhenNoBatchesActive()
    {
        var ctx = new HandlerContext();
        ctx.Batches.Setup(b => b.Dump()).Returns(Array.Empty<BatchAccumulatorSnapshotEntry>());

        await ctx.Handler.Consume(BuildContext(new FlushOverdueScoreBatchesCommand()));

        ctx.Batches.Verify(b => b.TakeBatch(It.IsAny<Guid>()), Times.Never);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class HandlerContext
    {
        public Mock<IPhoenixRecordRepository> Records { get; } = new();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<IDateTimeOffsetAccessor> DateTime { get; } = FakeDateTime.At(Now);
        public Mock<IBus> Bus { get; } = new();
        public Mock<IMessageScheduler> Scheduler { get; } = new();
        public Mock<IPlayerScoreBatchAccumulator> Batches { get; } = new();
        public Mock<IScoreJournalRepository> Journal { get; } = new();

        public UpdatePhoenixRecordHandler Handler { get; }

        public HandlerContext()
        {
            CurrentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(UserId).Build());
            Handler = new UpdatePhoenixRecordHandler(Records.Object, CurrentUser.Object, DateTime.Object,
                Bus.Object, Scheduler.Object, Batches.Object, Journal.Object);
        }

        public void GivenExistingScore(PhoenixScore score, PhoenixPlate plate, bool isBroken)
        {
            Records.Setup(r => r.GetRecordedScore(UserId, ChartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordedPhoenixScore(ChartId, score, plate, isBroken,
                    Now - TimeSpan.FromDays(1)));
        }
    }

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
