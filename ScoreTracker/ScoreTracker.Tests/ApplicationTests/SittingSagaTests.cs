using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Events;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SittingSagaTests
{
    private const string Source = "api:watcher-grab";
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IMessageScheduler> _scheduler = new();
    private readonly Mock<IScoreSessionRepository> _sessions = new();

    public SittingSagaTests()
    {
        _sessions.Setup(s => s.GetOpenSittings(It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OpenSitting>());
    }

    private SittingSaga Saga()
    {
        return new SittingSaga(_sessions.Object, _mediator.Object, _scheduler.Object, FakeDateTime.At(Now).Object,
            new SittingGate(), NullLogger<SittingSaga>.Instance);
    }

    private static RecordObservedPlaysCommand.ObservedPlay Play(DateTimeOffset playedAt, bool isBroken = false)
    {
        return new RecordObservedPlaysCommand.ObservedPlay(Guid.NewGuid(), 950000, PhoenixPlate.SuperbGame,
            isBroken, playedAt, null);
    }

    private static RecordSittingPlaysCommand Record(bool recordBrokenAsBest = false,
        params RecordObservedPlaysCommand.ObservedPlay[] plays)
    {
        return new RecordSittingPlaysCommand(UserId, MixEnum.Rise, Source, plays, recordBrokenAsBest);
    }

    private static RecordSittingPlaysCommand RecordOn(MixEnum mix, params RecordObservedPlaysCommand.ObservedPlay[] plays)
    {
        return new RecordSittingPlaysCommand(UserId, mix, Source, plays, false);
    }

    private static ScoreSessionRecord Sitting(Guid id, TimeSpan quietFor, int newCount = 0,
        DateTimeOffset? processedAt = null, MixEnum mix = MixEnum.Rise)
    {
        return new ScoreSessionRecord(id, UserId, mix, Source, null, null, Now.AddHours(-1),
            Now - quietFor, newCount, newCount, 0, processedAt);
    }

    private static ConsumeContext<T> ContextOf<T>(T message) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    [Fact]
    public async Task AFirstPlayOpensASittingAndRecordsUnderItWithoutTheTwoMinuteBatch()
    {
        var opened = Guid.Empty;
        _sessions.Setup(s => s.Open(It.IsAny<Guid>(), UserId, MixEnum.Rise, Source, null, null, Now,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, MixEnum, string, string?, string?, DateTimeOffset, CancellationToken>(
                (id, _, _, _, _, _, _, _) => opened = id)
            .Returns(Task.CompletedTask);
        var play = Play(Now.AddMinutes(-1));

        await Saga().Handle(Record(false, play), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, opened);
        _mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c =>
            c.ChartId == play.ChartId && c.SessionId == opened && c.DeferAnnouncement && c.KeepBestStats
            && c.RecordedAt == play.PlayedAt && c.Mix == MixEnum.Rise && c.Source == Source),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<RecordObservedPlaysCommand>(c =>
            c.SessionId == opened && c.Plays.Single() == play), It.IsAny<CancellationToken>()), Times.Once);
        _sessions.Verify(s => s.TouchArrival(opened, Now, It.IsAny<CancellationToken>()), Times.Once);
        _scheduler.Verify(s => s.SchedulePublish(
            Now.UtcDateTime + ScoreBatchPolicy.SittingQuietWindow(MixEnum.Rise) + ScoreBatchPolicy.DrainBuffer,
            It.Is<SittingSaga.CloseSittingCommand>(c => c.SessionId == opened && c.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APlayWithinTheGapJoinsTheOpenSitting()
    {
        var open = Guid.NewGuid();
        _sessions.Setup(s => s.GetOpenSittings(UserId, MixEnum.Rise,
                Now - ScoreBatchPolicy.SittingQuietWindow(MixEnum.Rise), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new OpenSitting(open, Now.AddMinutes(-40), Now.AddMinutes(-4)) });

        await Saga().Handle(Record(false, Play(Now.AddMinutes(-1))), CancellationToken.None);

        _sessions.Verify(s => s.Open(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(m => m.Send(It.Is<RecordObservedPlaysCommand>(c => c.SessionId == open),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABreakIsJournaledButOnlySeatedForAPlayerWhoSeatsBreaks()
    {
        var broken = Play(Now.AddMinutes(-2), isBroken: true);

        await Saga().Handle(Record(false, broken), CancellationToken.None);

        _mediator.Verify(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(m => m.Send(It.Is<RecordObservedPlaysCommand>(c => c.Plays.Single() == broken
            && !c.IncludeBroken), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AQuietSittingIsReplayedFromTheJournalAndAnnounced()
    {
        var id = Guid.NewGuid();
        _sessions.Setup(s => s.Get(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sitting(id, ScoreBatchPolicy.SittingQuietWindow(MixEnum.Rise)));
        _sessions.Setup(s => s.GetLastPlayedAt(UserId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Now.AddMinutes(-16));

        await Saga().Consume(ContextOf(new SittingSaga.CloseSittingCommand(UserId, MixEnum.Rise, id)));

        _mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c =>
                c.SessionId == id && c.UserId == UserId && c.Announce),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ASittingWhoseLastPlayIsMoreThanADayOldRecordsWithoutACard()
    {
        var id = Guid.NewGuid();
        _sessions.Setup(s => s.Get(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sitting(id, ScoreBatchPolicy.SittingQuietWindow(MixEnum.Rise)));
        _sessions.Setup(s => s.GetLastPlayedAt(UserId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Now.AddDays(-2));

        await Saga().Consume(ContextOf(new SittingSaga.CloseSittingCommand(UserId, MixEnum.Rise, id)));

        _mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == id && !c.Announce),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACloseThatFindsANewerArrivalLeavesTheSittingOpen()
    {
        var id = Guid.NewGuid();
        _sessions.Setup(s => s.Get(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sitting(id, TimeSpan.FromMinutes(4)));

        await Saga().Consume(ContextOf(new SittingSaga.CloseSittingCommand(UserId, MixEnum.Rise, id)));

        _mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ASittingAlreadyReplayedOrAnnouncedIsNeverAnnouncedAgain()
    {
        var replayed = Guid.NewGuid();
        var announced = Guid.NewGuid();
        _sessions.Setup(s => s.Get(replayed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sitting(replayed, TimeSpan.FromHours(1), newCount: 3));
        _sessions.Setup(s => s.Get(announced, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sitting(announced, TimeSpan.FromHours(1), processedAt: Now.AddMinutes(-30)));

        await Saga().Consume(ContextOf(new SittingSaga.CloseSittingCommand(UserId, MixEnum.Rise, replayed)));
        await Saga().Consume(ContextOf(new SittingSaga.CloseSittingCommand(UserId, MixEnum.Rise, announced)));

        _mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TheSweepClosesSittingsWhoseScheduledCloseWasLost()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _sessions.Setup(s => s.ListOverdueSittings(It.IsAny<IReadOnlyCollection<MixEnum>>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoreSessionRecord>());
        _sessions.Setup(s => s.ListOverdueSittings(
                It.Is<IReadOnlyCollection<MixEnum>>(m => m.Contains(MixEnum.Rise)),
                Now - ScoreBatchPolicy.SittingOverdueAfter(MixEnum.Rise), 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Sitting(first, TimeSpan.FromHours(6)), Sitting(second, TimeSpan.FromHours(5)) });
        _sessions.Setup(s => s.Get(first, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sitting(first, TimeSpan.FromHours(6)));
        _sessions.Setup(s => s.Get(second, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sitting(second, TimeSpan.FromHours(5)));

        await Saga().Consume(ContextOf(new OverdueScoreBatchesFlushedEvent(Now)));

        _mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == first),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<ReplaySessionCommand>(c => c.SessionId == second),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheSweepWaitsOutEachMixsOwnWindow()
    {
        // RISE keeps a quiet sitting open for 4 hours, the pad mixes for 15 minutes, and the sweep
        // gives each its own ask so the long window never crowds the short one out of the cap.
        _sessions.Setup(s => s.ListOverdueSittings(It.IsAny<IReadOnlyCollection<MixEnum>>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoreSessionRecord>());

        await Saga().Consume(ContextOf(new OverdueScoreBatchesFlushedEvent(Now)));

        _sessions.Verify(s => s.ListOverdueSittings(
            It.Is<IReadOnlyCollection<MixEnum>>(m =>
                m.Count == 2 && m.Contains(MixEnum.Rise) && m.Contains(MixEnum.RiseArcade)),
            Now - TimeSpan.FromHours(4) - TimeSpan.FromMinutes(5), 25, It.IsAny<CancellationToken>()), Times.Once);
        _sessions.Verify(s => s.ListOverdueSittings(
            It.Is<IReadOnlyCollection<MixEnum>>(m =>
                m.Count == 2 && m.Contains(MixEnum.Phoenix) && m.Contains(MixEnum.Phoenix2)),
            Now - TimeSpan.FromMinutes(20), 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARisePlayHoursAfterTheLastStillJoinsTheOpenSitting()
    {
        // Three hours away from the desk is still one evening on RISE (docs/design/rise.md D25), and
        // the sitting's close moves out to 4 hours past this arrival.
        var open = Guid.NewGuid();
        _sessions.Setup(s => s.GetOpenSittings(UserId, MixEnum.Rise, Now - TimeSpan.FromHours(4),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new OpenSitting(open, Now.AddHours(-4), Now.AddHours(-3).AddMinutes(-10)) });

        await Saga().Handle(Record(false, Play(Now.AddMinutes(-10))), CancellationToken.None);

        _sessions.Verify(s => s.Open(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<MixEnum>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(m => m.Send(It.Is<RecordObservedPlaysCommand>(c => c.SessionId == open),
            It.IsAny<CancellationToken>()), Times.Once);
        _scheduler.Verify(s => s.SchedulePublish(
            Now.UtcDateTime + TimeSpan.FromHours(4) + ScoreBatchPolicy.DrainBuffer,
            It.Is<SittingSaga.CloseSittingCommand>(c => c.SessionId == open), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ARiseSittingQuietForAnHourIsNotClosedByItsTimer()
    {
        var id = Guid.NewGuid();
        _sessions.Setup(s => s.Get(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sitting(id, TimeSpan.FromHours(1)));

        await Saga().Consume(ContextOf(new SittingSaga.CloseSittingCommand(UserId, MixEnum.Rise, id)));

        _mediator.Verify(m => m.Send(It.IsAny<ReplaySessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task APhoenix2SittingKeepsItsFifteenMinutes()
    {
        // D25 is RISE's alone: on Phoenix 2 a play 25 minutes after the last starts a sitting of its
        // own, and that sitting closes 15 minutes after this arrival.
        var earlier = Guid.NewGuid();
        _sessions.Setup(s => s.GetOpenSittings(UserId, MixEnum.Phoenix2, Now - TimeSpan.FromMinutes(15),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new OpenSitting(earlier, Now.AddMinutes(-40), Now.AddMinutes(-26)) });

        await Saga().Handle(RecordOn(MixEnum.Phoenix2, Play(Now.AddMinutes(-1))), CancellationToken.None);

        _sessions.Verify(s => s.Open(It.IsAny<Guid>(), UserId, MixEnum.Phoenix2, Source, null, null, Now,
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<RecordObservedPlaysCommand>(c => c.SessionId == earlier),
            It.IsAny<CancellationToken>()), Times.Never);
        _scheduler.Verify(s => s.SchedulePublish(
            Now.UtcDateTime + TimeSpan.FromMinutes(15) + ScoreBatchPolicy.DrainBuffer,
            It.IsAny<SittingSaga.CloseSittingCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheSweepAsksAboutEveryPhoenixScoredMixAndNoOther()
    {
        _sessions.Setup(s => s.ListOverdueSittings(It.IsAny<IReadOnlyCollection<MixEnum>>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoreSessionRecord>());

        await Saga().Consume(ContextOf(new OverdueScoreBatchesFlushedEvent(Now)));

        var asked = _sessions.Invocations
            .Where(i => i.Method.Name == nameof(IScoreSessionRepository.ListOverdueSittings))
            .SelectMany(i => (IReadOnlyCollection<MixEnum>)i.Arguments[0])
            .ToArray();
        Assert.Equal(Enum.GetValues<MixEnum>().Where(m => !m.UsesLegacyScoring()).OrderBy(m => m),
            asked.OrderBy(m => m));
    }
}
