using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class MarchOfMurlocsHandlerTests
{
    private static TournamentRecord MoM(DateTimeOffset? endDate, bool isHighlighted = true) =>
        new(Guid.NewGuid(), Name.From("MoM"), 0, TournamentType.Stamina, "Online",
            isHighlighted, null, null, endDate, IsMoM: true);

    private static Mock<ConsumeContext<T>> ContextOf<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx;
    }

    [Fact]
    public async Task TryScheduleCyclesImmediatelyWhenNoActiveMoMExists()
    {
        var tournaments = new Mock<ITournamentRepository>();
        var charts = new Mock<IChartRepository>();
        var bus = new Mock<IBus>();
        var scheduler = new Mock<IMessageScheduler>();
        var dateTime = FakeDateTime.At(2026, 4, 1);

        tournaments.Setup(t => t.GetAllTournaments(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TournamentRecord>());

        var handler = new MarchOfMurlocsHandler(tournaments.Object, charts.Object, bus.Object,
            scheduler.Object, dateTime.Object);

        await handler.Consume(ContextOf(new MarchOfMurlocsHandler.TryScheduleMoM()).Object);

        bus.Verify(b => b.Publish(It.IsAny<MarchOfMurlocsHandler.CycleMoM>(), It.IsAny<CancellationToken>()),
            Times.Once);
        scheduler.Verify(s => s.SchedulePublish(It.IsAny<DateTime>(),
                It.IsAny<MarchOfMurlocsHandler.CycleMoM>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryScheduleCyclesImmediatelyWhenActiveMoMHasAlreadyEnded()
    {
        var tournaments = new Mock<ITournamentRepository>();
        var charts = new Mock<IChartRepository>();
        var bus = new Mock<IBus>();
        var scheduler = new Mock<IMessageScheduler>();
        var now = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var dateTime = FakeDateTime.At(now);

        var endedMoM = MoM(now - TimeSpan.FromDays(1));
        tournaments.Setup(t => t.GetAllTournaments(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { endedMoM });

        var handler = new MarchOfMurlocsHandler(tournaments.Object, charts.Object, bus.Object,
            scheduler.Object, dateTime.Object);

        await handler.Consume(ContextOf(new MarchOfMurlocsHandler.TryScheduleMoM()).Object);

        bus.Verify(b => b.Publish(It.IsAny<MarchOfMurlocsHandler.CycleMoM>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TrySchedulePostponesCycleUntilCurrentMoMEnds()
    {
        var tournaments = new Mock<ITournamentRepository>();
        var charts = new Mock<IChartRepository>();
        var bus = new Mock<IBus>();
        var scheduler = new Mock<IMessageScheduler>();
        var now = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var endDate = now + TimeSpan.FromDays(30);
        var dateTime = FakeDateTime.At(now);

        tournaments.Setup(t => t.GetAllTournaments(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MoM(endDate) });

        var handler = new MarchOfMurlocsHandler(tournaments.Object, charts.Object, bus.Object,
            scheduler.Object, dateTime.Object);

        await handler.Consume(ContextOf(new MarchOfMurlocsHandler.TryScheduleMoM()).Object);

        bus.Verify(b => b.Publish(It.IsAny<MarchOfMurlocsHandler.CycleMoM>(), It.IsAny<CancellationToken>()),
            Times.Never);
        scheduler.Verify(s => s.SchedulePublish(
                (endDate + TimeSpan.FromMinutes(1)).DateTime,
                It.IsAny<MarchOfMurlocsHandler.CycleMoM>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CycleCreatesSingleAndDoubleTournamentsAndUnhighlightsPreviousMoMs()
    {
        var tournaments = new Mock<ITournamentRepository>();
        var charts = new Mock<IChartRepository>();
        var bus = new Mock<IBus>();
        var scheduler = new Mock<IMessageScheduler>();
        // Previous MoM ended in March → new season is "Spring" with end in June.
        var previousEnd = new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.FromHours(-5));
        var now = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var dateTime = FakeDateTime.At(now);
        var previousMoM = MoM(previousEnd);

        tournaments.Setup(t => t.GetAllTournaments(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { previousMoM });
        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build(),
                new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build(),
                new ChartBuilder().WithLevel(20).WithType(ChartType.CoOp).Build()
            });

        var savedConfigurations = new List<TournamentConfiguration>();
        var savedRecords = new List<TournamentRecord>();
        tournaments.Setup(t => t.CreateOrSaveTournament(It.IsAny<TournamentConfiguration>(),
                It.IsAny<CancellationToken>()))
            .Callback<TournamentConfiguration, CancellationToken>((cfg, _) => savedConfigurations.Add(cfg))
            .Returns(Task.CompletedTask);
        tournaments.Setup(t => t.CreateOrSaveTournament(It.IsAny<TournamentRecord>(),
                It.IsAny<CancellationToken>()))
            .Callback<TournamentRecord, CancellationToken>((rec, _) => savedRecords.Add(rec))
            .Returns(Task.CompletedTask);

        var handler = new MarchOfMurlocsHandler(tournaments.Object, charts.Object, bus.Object,
            scheduler.Object, dateTime.Object);

        await handler.Consume(ContextOf(new MarchOfMurlocsHandler.CycleMoM()).Object);

        var newTournaments = savedConfigurations.Where(s => s.IsMom).ToArray();
        Assert.Equal(2, newTournaments.Length);
        Assert.Contains(newTournaments, t => ((string)t.Name).Contains("Spring") && ((string)t.Name).Contains("Singles"));
        Assert.Contains(newTournaments, t => ((string)t.Name).Contains("Spring") && ((string)t.Name).Contains("Doubles"));
        Assert.All(newTournaments, t => Assert.Equal(6, t.EndDate!.Value.Month));

        tournaments.Verify(t => t.CreateScoringLevelSnapshots(It.IsAny<Guid>(),
                It.IsAny<IEnumerable<(Guid, double)>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        Assert.Contains(savedRecords, r => r.Id == previousMoM.Id && !r.IsHighlighted);
    }
}
