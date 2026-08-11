using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The Your Data cleanup for records made while "Record broken scores as your best" was on.
///     The real-database proof that it removes those rows and only those rows lives in
///     Tests.Integration — a mock cannot catch an over-delete.
/// </summary>
public sealed class BrokenRecordCleanupSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private static (BrokenRecordCleanupSaga Saga, Mock<IPhoenixRecordRepository> Records, Mock<IBus> Bus) Build()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        var bus = new Mock<IBus>();
        return (new BrokenRecordCleanupSaga(records.Object, bus.Object, FakeDateTime.At(Now).Object), records, bus);
    }

    [Fact]
    public async Task TheCountCoversEveryPhoenixScoringMixAndNoLegacyOne()
    {
        // Shown at zero rather than hidden, so the card can say "nothing to clean up" instead of
        // looking like it forgot to check. A legacy mix records a letter grade with no broken
        // flag, so it is never asked.
        var (saga, records, _) = Build();
        records.Setup(r => r.CountBrokenRecords(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var counts = await saga.Handle(new GetBrokenRecordCountsQuery(UserId), CancellationToken.None);

        Assert.Contains(counts, c => c.Mix == MixEnum.Phoenix);
        Assert.Contains(counts, c => c.Mix == MixEnum.Phoenix2);
        Assert.DoesNotContain(counts, c => c.Mix.UsesLegacyScoring());
        Assert.All(counts, c => Assert.Equal(0, c.Count));
    }

    [Fact]
    public async Task TheCountIsWhateverTheLedgerHolds()
    {
        var (saga, records, _) = Build();
        records.Setup(r => r.CountBrokenRecords(MixEnum.Phoenix2, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(268);
        records.Setup(r => r.CountBrokenRecords(It.Is<MixEnum>(m => m != MixEnum.Phoenix2), UserId,
            It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var counts = await saga.Handle(new GetBrokenRecordCountsQuery(UserId), CancellationToken.None);

        Assert.Equal(268, counts.Single(c => c.Mix == MixEnum.Phoenix2).Count);
        Assert.Equal(3, counts.Single(c => c.Mix == MixEnum.Phoenix).Count);
    }

    [Fact]
    public async Task CleaningUpRemovesOnlyTheRecordsAndReportsHowMany()
    {
        var (saga, records, _) = Build();
        records.Setup(r => r.DeleteBrokenRecords(MixEnum.Phoenix2, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(268);

        var removed = await saga.Handle(new DeleteBrokenRecordsCommand(UserId, new[] { MixEnum.Phoenix2 }),
            CancellationToken.None);

        Assert.Equal(268, removed);
        records.Verify(r => r.DeleteBrokenRecords(MixEnum.Phoenix2, UserId, It.IsAny<CancellationToken>()),
            Times.Once);
        records.Verify(r => r.DeleteAllForUser(It.IsAny<Guid>(), It.IsAny<MixEnum?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EachMixThatLostRowsAnnouncesItSoDerivedStateRecomputes()
    {
        // Pumbility, titles and folder lamps are computed from the records that just went, so the
        // empty-change announcement is how they are told to recompute from what is left.
        var (saga, records, bus) = Build();
        records.Setup(r => r.DeleteBrokenRecords(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await saga.Handle(new DeleteBrokenRecordsCommand(UserId, new[] { MixEnum.Phoenix, MixEnum.Phoenix2 }),
            CancellationToken.None);

        foreach (var mix in new[] { MixEnum.Phoenix, MixEnum.Phoenix2 })
            bus.Verify(b => b.Publish(
                It.Is<PlayerScoresUpdatedEvent>(e => e.UserId == UserId && e.Mix == mix && !e.Changes.Any()),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AMixWithNothingToCleanUpAnnouncesNothing()
    {
        // The announcement ends in a rating and title recompute, so firing it for a mix that lost
        // no rows would spend that work — and risk a milestone — on a no-op.
        var (saga, records, bus) = Build();
        records.Setup(r => r.DeleteBrokenRecords(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var removed = await saga.Handle(new DeleteBrokenRecordsCommand(UserId, new[] { MixEnum.Phoenix2 }),
            CancellationToken.None);

        Assert.Equal(0, removed);
        bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ALegacyMixIsNeverTouched()
    {
        // XX records a letter grade in BestAttempt, which carries no failed-stage flag — there is
        // nothing there to withdraw, so the request is dropped rather than run against it.
        var (saga, records, bus) = Build();

        var removed = await saga.Handle(new DeleteBrokenRecordsCommand(UserId, new[] { MixEnum.XX }),
            CancellationToken.None);

        Assert.Equal(0, removed);
        records.Verify(r => r.DeleteBrokenRecords(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        bus.Verify(b => b.Publish(It.IsAny<PlayerScoresUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
