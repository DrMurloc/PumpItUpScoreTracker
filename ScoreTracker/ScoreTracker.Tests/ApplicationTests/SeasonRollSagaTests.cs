using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Seasons.Application;
using ScoreTracker.Seasons.Contracts.Events;
using ScoreTracker.Seasons.Contracts.Messages;
using ScoreTracker.Seasons.Contracts.Queries;
using ScoreTracker.Seasons.Domain;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class SeasonRollSagaTests
{
    private static readonly SeasonId Summer = SeasonId.From(2026, 3);
    private static readonly SeasonId Fall = SeasonId.From(2026, 4);

    // Summer 2026 ends 2026-09-30 23:59:59 UTC-5, which is 04:59:59Z on 1 October. There is no
    // grace (D13), so the seal falls due the very next second: at this instant the season is over
    // and owed its stamp, and a second earlier it is still running.
    private static readonly DateTimeOffset SealDue = new(2026, 10, 1, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheFirstRollOfAQuarterOpensItWithMoMsWindowAndAnnouncesIt()
    {
        var seasons = new Mock<ISeasonRepository>();
        seasons.Setup(s => s.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SeasonRecord>());
        var context = Context();

        await Saga(seasons, new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)).Consume(context.Object);

        seasons.Verify(s => s.Add(It.Is<SeasonRecord>(r =>
            r.Id == Fall && r.Name == "Fall 2026" && !r.IsBalanced && r.SealedAt == null &&
            r.StartsAt == new DateTimeOffset(2026, 10, 1, 0, 0, 0, SeasonCalendar.Offset) &&
            r.EndsAt == new DateTimeOffset(2026, 12, 31, 23, 59, 59, SeasonCalendar.Offset)), It.IsAny<CancellationToken>()), Times.Once);
        context.Verify(c => c.Publish(It.Is<SeasonOpenedEvent>(e => e.Season == Fall), It.IsAny<CancellationToken>()), Times.Once);
        seasons.Verify(s => s.Seal(It.IsAny<SeasonId>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASecondRollTheSameDayChangesNothing()
    {
        var seasons = new Mock<ISeasonRepository>();
        seasons.Setup(s => s.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SeasonRecord> { SeasonRollSaga.Open(Fall) });
        var context = Context();

        await Saga(seasons, new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)).Consume(context.Object);

        seasons.Verify(s => s.Add(It.IsAny<SeasonRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        seasons.Verify(s => s.Seal(It.IsAny<SeasonId>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Verify(c => c.Publish(It.IsAny<SeasonOpenedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Verify(c => c.Publish(It.IsAny<SeasonSealedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnEndedSeasonIsSealedTheSecondItsWindowClosesAndNotBefore()
    {
        var seasons = new Mock<ISeasonRepository>();
        seasons.Setup(s => s.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SeasonRecord> { SeasonRollSaga.Open(Fall), SeasonRollSaga.Open(Summer) });

        await Saga(seasons, SealDue.AddSeconds(-1)).Consume(Context().Object);
        seasons.Verify(s => s.Seal(It.IsAny<SeasonId>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);

        var sealedAt = SealDue;
        var context = Context();
        await Saga(seasons, sealedAt).Consume(context.Object);

        seasons.Verify(s => s.Seal(Summer, sealedAt, It.IsAny<CancellationToken>()), Times.Once);
        seasons.Verify(s => s.Seal(Fall, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Verify(c => c.Publish(It.Is<SeasonSealedEvent>(e => e.Season == Summer && e.SealedAt == sealedAt),
            It.IsAny<CancellationToken>()), Times.Once);
        seasons.Verify(s => s.Add(It.IsAny<SeasonRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASealedSeasonIsNeverSealedAgain()
    {
        var seasons = new Mock<ISeasonRepository>();
        seasons.Setup(s => s.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SeasonRecord>
        {
            SeasonRollSaga.Open(Fall),
            SeasonRollSaga.Open(Summer) with { SealedAt = SealDue }
        });
        var context = Context();

        await Saga(seasons, SealDue.AddDays(3)).Consume(context.Object);

        seasons.Verify(s => s.Seal(It.IsAny<SeasonId>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Verify(c => c.Publish(It.IsAny<SeasonOpenedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Verify(c => c.Publish(It.IsAny<SeasonSealedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheBackfillCreatesEveryQuarterSinceSummer2026AndAsksEachForItsReplay()
    {
        var seasons = new Mock<ISeasonRepository>();
        seasons.Setup(s => s.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SeasonRecord>());
        var context = BackfillContext();

        // Standing in Fall 2026: Summer is the first season there has ever been, Fall the second.
        await Saga(seasons, new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)).Consume(context.Object);

        seasons.Verify(s => s.Add(It.Is<SeasonRecord>(r => r.Id == Summer), It.IsAny<CancellationToken>()), Times.Once);
        seasons.Verify(s => s.Add(It.Is<SeasonRecord>(r => r.Id == Fall), It.IsAny<CancellationToken>()), Times.Once);
        seasons.Verify(s => s.Add(It.IsAny<SeasonRecord>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        context.Verify(c => c.Publish(It.Is<SeasonBackfillRequestedEvent>(x => x.Season == Summer),
            It.IsAny<CancellationToken>()), Times.Once);
        context.Verify(c => c.Publish(It.Is<SeasonBackfillRequestedEvent>(x => x.Season == Fall),
            It.IsAny<CancellationToken>()), Times.Once);
        // The backfill never seals; the next roll stamps the ended quarters (D37).
        seasons.Verify(s => s.Seal(It.IsAny<SeasonId>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunningTheBackfillTwiceCreatesNothingASecondTimeAndSkipsWhatIsSealed()
    {
        var seasons = new Mock<ISeasonRepository>();
        seasons.Setup(s => s.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync(new List<SeasonRecord>
        {
            SeasonRollSaga.Open(Summer) with { SealedAt = SealDue },
            SeasonRollSaga.Open(Fall)
        });
        var context = BackfillContext();

        await Saga(seasons, new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)).Consume(context.Object);

        seasons.Verify(s => s.Add(It.IsAny<SeasonRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        // A sealed season is immutable, so there is nothing to replay into it (D13).
        context.Verify(c => c.Publish(It.Is<SeasonBackfillRequestedEvent>(x => x.Season == Summer),
            It.IsAny<CancellationToken>()), Times.Never);
        context.Verify(c => c.Publish(It.Is<SeasonBackfillRequestedEvent>(x => x.Season == Fall),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ConsumeContext<BackfillSeasonsCommand>> BackfillContext()
    {
        var context = new Mock<ConsumeContext<BackfillSeasonsCommand>>();
        context.SetupGet(c => c.Message).Returns(new BackfillSeasonsCommand());
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task TheConsoleReadsTheCalendarStraightFromTheRepository()
    {
        var seasons = new Mock<ISeasonRepository>();
        var calendar = new List<SeasonRecord> { SeasonRollSaga.Open(Fall), SeasonRollSaga.Open(Summer) };
        seasons.Setup(s => s.GetAll(It.IsAny<CancellationToken>())).ReturnsAsync(calendar);

        var read = await new SeasonQueryHandler(seasons.Object).Handle(new GetSeasonsQuery(), CancellationToken.None);

        Assert.Equal(calendar, read);
    }

    private static SeasonRollSaga Saga(Mock<ISeasonRepository> seasons, DateTimeOffset now)
    {
        return new SeasonRollSaga(seasons.Object, FakeDateTime.At(now).Object, NullLogger<SeasonRollSaga>.Instance);
    }

    private static Mock<ConsumeContext<RollSeasonCommand>> Context()
    {
        var context = new Mock<ConsumeContext<RollSeasonCommand>>();
        context.SetupGet(c => c.Message).Returns(new RollSeasonCommand());
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}
