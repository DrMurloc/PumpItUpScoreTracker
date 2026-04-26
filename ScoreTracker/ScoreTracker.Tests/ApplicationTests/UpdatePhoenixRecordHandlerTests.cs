using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
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
    public async Task NewClearSavesRecordSchedulesFireAndRecordsNewChart()
    {
        var ctx = new HandlerContext();
        ctx.Batches.Setup(b => b.RegisterFireAt(UserId, It.IsAny<DateTime>())).Returns(true);

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
            It.Is<UpdatePhoenixRecordHandler.TryFireScoreMessage>(m => m.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Batches.Verify(b => b.RecordNewChart(UserId, ChartId), Times.Once);
        ctx.Batches.Verify(b => b.RecordUpscoreIfNotNew(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<PhoenixScore>()), Times.Never);
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
    public async Task NewClearWhenExistingWasBrokenRecordsNewChart()
    {
        var ctx = new HandlerContext();
        // Existing was broken at the same score → transition to a clear is a new clear,
        // and 950000 < 950000 is false so it is NOT also an upscore.
        ctx.GivenExistingScore(score: 950000, plate: PhoenixPlate.SuperbGame, isBroken: true);
        ctx.Batches.Setup(b => b.RegisterFireAt(UserId, It.IsAny<DateTime>())).Returns(true);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Batches.Verify(b => b.RecordNewChart(UserId, ChartId), Times.Once);
        ctx.Batches.Verify(b => b.RecordUpscoreIfNotNew(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<PhoenixScore>()), Times.Never);
    }

    [Fact]
    public async Task ClearedWithHigherScoreFromBrokenIsBothNewChartAndUpscore()
    {
        // When transitioning from broken→clear with a higher score, both branches fire;
        // the accumulator (per its contract) takes new-clear precedence.
        var ctx = new HandlerContext();
        ctx.GivenExistingScore(score: 700000, plate: PhoenixPlate.RoughGame, isBroken: true);
        ctx.Batches.Setup(b => b.RegisterFireAt(UserId, It.IsAny<DateTime>())).Returns(true);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Batches.Verify(b => b.RecordNewChart(UserId, ChartId), Times.Once);
        ctx.Batches.Verify(b => b.RecordUpscoreIfNotNew(UserId, ChartId,
            It.Is<PhoenixScore>(s => s == (PhoenixScore)700000)), Times.Once);
    }

    [Fact]
    public async Task UpscoreRecordsUpscoreWithPreviousScore()
    {
        var ctx = new HandlerContext();
        ctx.GivenExistingScore(score: 900000, plate: PhoenixPlate.SuperbGame, isBroken: false);
        ctx.Batches.Setup(b => b.RegisterFireAt(UserId, It.IsAny<DateTime>())).Returns(true);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 970000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Batches.Verify(b => b.RecordUpscoreIfNotNew(UserId, ChartId,
            It.Is<PhoenixScore>(s => s == (PhoenixScore)900000)), Times.Once);
        ctx.Batches.Verify(b => b.RecordNewChart(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
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

        ctx.Batches.Verify(b => b.RegisterFireAt(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
        ctx.Scheduler.Verify(s => s.SchedulePublish(
            It.IsAny<DateTime>(),
            It.IsAny<UpdatePhoenixRecordHandler.TryFireScoreMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Batches.Verify(b => b.RecordNewChart(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        ctx.Batches.Verify(b => b.RecordUpscoreIfNotNew(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<PhoenixScore>()), Times.Never);
    }

    [Fact]
    public async Task SkipsSchedulingWhenBatchAlreadyActiveButStillRecords()
    {
        var ctx = new HandlerContext();
        // Batch already active: RegisterFireAt returns false, so the handler must NOT
        // schedule a new fire message — but it must still record the new clear.
        ctx.Batches.Setup(b => b.RegisterFireAt(UserId, It.IsAny<DateTime>())).Returns(false);

        await ctx.Handler.Handle(
            new UpdatePhoenixBestAttemptCommand(ChartId, IsBroken: false, Score: 950000,
                Plate: PhoenixPlate.SuperbGame),
            CancellationToken.None);

        ctx.Scheduler.Verify(s => s.SchedulePublish(
            It.IsAny<DateTime>(),
            It.IsAny<UpdatePhoenixRecordHandler.TryFireScoreMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Batches.Verify(b => b.RecordNewChart(UserId, ChartId), Times.Once);
    }

    [Fact]
    public async Task ReschedulesAndDoesNotPublishWhenFireAtIsInTheFuture()
    {
        var ctx = new HandlerContext();
        var fireAt = Now.UtcDateTime + TimeSpan.FromMinutes(1);
        ctx.Batches.Setup(b => b.GetFireAt(UserId)).Returns(fireAt);

        await ctx.Handler.Consume(BuildContext(new UpdatePhoenixRecordHandler.TryFireScoreMessage(UserId)));

        ctx.Scheduler.Verify(s => s.SchedulePublish(
            fireAt + TimeSpan.FromMinutes(2),
            It.Is<UpdatePhoenixRecordHandler.TryFireScoreMessage>(m => m.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<PlayerScoreUpdatedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Batches.Verify(b => b.TakeBatch(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task TakesBatchAndPublishesWhenFireAtHasBeenReached()
    {
        var ctx = new HandlerContext();
        var fireAt = Now.UtcDateTime - TimeSpan.FromSeconds(1); // already past
        var newCharts = new[] { Guid.NewGuid() };
        var upscored = new System.Collections.Generic.Dictionary<Guid, int> { { Guid.NewGuid(), 900000 } };
        ctx.Batches.Setup(b => b.GetFireAt(UserId)).Returns(fireAt);
        ctx.Batches.Setup(b => b.TakeBatch(UserId)).Returns(new PendingScoreBatch(newCharts, upscored));

        await ctx.Handler.Consume(BuildContext(new UpdatePhoenixRecordHandler.TryFireScoreMessage(UserId)));

        ctx.Bus.Verify(b => b.Publish(
            It.Is<PlayerScoreUpdatedEvent>(e => e.UserId == UserId
                                                && e.NewChartIds == newCharts
                                                && e.UpscoredChartIds == upscored),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Scheduler.Verify(s => s.SchedulePublish(
            It.IsAny<DateTime>(),
            It.IsAny<UpdatePhoenixRecordHandler.TryFireScoreMessage>(),
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

        public UpdatePhoenixRecordHandler Handler { get; }

        public HandlerContext()
        {
            CurrentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(UserId).Build());
            Handler = new UpdatePhoenixRecordHandler(Records.Object, CurrentUser.Object, DateTime.Object,
                Bus.Object, Scheduler.Object, Batches.Object);
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
