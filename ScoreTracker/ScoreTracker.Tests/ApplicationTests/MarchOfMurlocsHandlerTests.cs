using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts.Messages;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The cycle is stateless against MoMSeason (D2): the only question either consumer asks
///     is "does the quarter we are standing in have its season". History never enters into
///     it, which is what makes the two runaway variants (the missing month-map arm and the
///     late-cycle year drift) structurally impossible rather than merely fixed.
/// </summary>
public sealed class MarchOfMurlocsHandlerTests
{
    private static MoMSeason Season(int year, int quarter, DateTimeOffset endsAt) =>
        new(Guid.NewGuid(), year, (byte)quarter, $"Season {year}/{quarter}",
            endsAt.AddMonths(-3), endsAt, endsAt.AddMonths(-3));

    private static Mock<ConsumeContext<T>> ContextOf<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx;
    }

    private static MarchOfMurlocsHandler Handler(Mock<IMoMRepository> mom, Mock<IBus> bus,
        Mock<IMessageScheduler> scheduler, DateTimeOffset now)
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(r => r.GetCharts(It.IsAny<MixEnum>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build(),
                new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build(),
                new ChartBuilder().WithLevel(20).WithType(ChartType.CoOp).Build()
            });
        var scoringLevels = new Mock<IChartScoringLevelRepository>();
        scoringLevels.Setup(s => s.GetScoringLevels(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        // Unless a test says otherwise, a season already holds all four boards: the heal has
        // nothing to do and the older try-schedule tests keep their meaning.
        if (mom.Setups.All(setup => !setup.ToString().Contains(nameof(IMoMRepository.GetBoardKeys))))
            mom.Setup(m => m.GetBoardKeys(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(AllFour);
        return new MarchOfMurlocsHandler(mom.Object, charts.Object, bus.Object, scheduler.Object,
            FakeDateTime.At(now).Object, scoringLevels.Object);
    }

    private static readonly IReadOnlyList<MoMBoardKey> AllFour = new[]
    {
        new MoMBoardKey(MixEnum.Phoenix, ChartType.Double), new MoMBoardKey(MixEnum.Phoenix, ChartType.Single),
        new MoMBoardKey(MixEnum.Phoenix2, ChartType.Double), new MoMBoardKey(MixEnum.Phoenix2, ChartType.Single)
    };

    [Fact]
    public async Task TryScheduleCyclesImmediatelyWhenTheCurrentQuarterHasNoSeason()
    {
        var mom = new Mock<IMoMRepository>();
        var bus = new Mock<IBus>();
        var scheduler = new Mock<IMessageScheduler>();
        mom.Setup(m => m.GetSeason(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoMSeason?)null);

        var handler = Handler(mom, bus, scheduler, new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
        await handler.Consume(ContextOf(new TryScheduleMoMCommand()).Object);

        bus.Verify(b => b.Publish(It.IsAny<CycleMoMCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
        scheduler.Verify(s => s.SchedulePublish(It.IsAny<DateTime>(),
                It.IsAny<CycleMoMCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // April sits in Q2 — the lookup must be for the quarter we are standing in.
        mom.Verify(m => m.GetSeason(2026, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrySchedulePostponesTheCycleUntilTheCurrentSeasonEnds()
    {
        var mom = new Mock<IMoMRepository>();
        var bus = new Mock<IBus>();
        var scheduler = new Mock<IMessageScheduler>();
        var endsAt = new DateTimeOffset(2026, 6, 30, 23, 59, 59, TimeSpan.FromHours(-5));
        mom.Setup(m => m.GetSeason(2026, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Season(2026, 2, endsAt));

        var handler = Handler(mom, bus, scheduler, new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero));
        await handler.Consume(ContextOf(new TryScheduleMoMCommand()).Object);

        bus.Verify(b => b.Publish(It.IsAny<CycleMoMCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // The UTC instant, not the UTC-5 wall clock — the scheduler compares against UTC, and
        // the bare .DateTime would have fired five hours early.
        scheduler.Verify(s => s.SchedulePublish(
                (endsAt + TimeSpan.FromMinutes(1)).UtcDateTime,
                It.IsAny<CycleMoMCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CycleDoesNothingWhenTheCurrentQuarterAlreadyHasItsSeason()
    {
        // Idempotency for a duplicated CycleMoMCommand (in-memory transport replay, double
        // publish); the filtered unique (Year, Quarter) index is the hard guarantee behind it.
        var mom = new Mock<IMoMRepository>();
        var now = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        mom.Setup(m => m.GetSeason(2026, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Season(2026, 2, new DateTimeOffset(2026, 6, 30, 23, 59, 59, TimeSpan.FromHours(-5))));

        var handler = Handler(mom, new Mock<IBus>(), new Mock<IMessageScheduler>(), now);
        await handler.Consume(ContextOf(new CycleMoMCommand()).Object);

        mom.Verify(m => m.CreateSeason(It.IsAny<MoMSeason>(), It.IsAny<IReadOnlyList<MoMBoardSeed>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        mom.Verify(m => m.PruneEndedEmptySeasons(It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CycleCreatesTheCurrentQuarterSeasonWithABoardPerMixAndChartType()
    {
        var (season, boards) = await Cycle(new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero));

        Assert.Equal("Summer 2026", season.Name);
        Assert.Equal(2026, season.Year);
        Assert.Equal((byte)3, season.Quarter);
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(-5)), season.EndsAt);

        Assert.Equal(4, boards.Count);
        Assert.Equal(AllFour, boards.Select(b => new MoMBoardKey(b.Mix, b.ChartType)).ToArray());
        var singles = Assert.Single(boards, b => b.Mix == MixEnum.Phoenix && b.ChartType == ChartType.Single);
        Assert.Equal("March of Murlocs Summer 2026 - Singles", (string)singles.Configuration.Name);
        var doubles = Assert.Single(boards, b => b.Mix == MixEnum.Phoenix && b.ChartType == ChartType.Double);
        Assert.Equal("March of Murlocs Summer 2026 - Doubles", (string)doubles.Configuration.Name);
        var phoenix2 = Assert.Single(boards, b => b.Mix == MixEnum.Phoenix2 && b.ChartType == ChartType.Double);
        Assert.Equal("March of Murlocs Summer 2026 - Doubles (Phoenix 2)", (string)phoenix2.Configuration.Name);
        // The Phoenix 2 tuning of PUMBILITY+ (D41), graded on Phoenix 2's own cutoffs.
        Assert.Equal(MixEnum.Phoenix2, phoenix2.Configuration.Scoring.Mix);
        Assert.Equal(.70, phoenix2.Configuration.Scoring.LetterGradeModifiers[PhoenixLetterGrade.APlus]);
        Assert.Equal(1.10, phoenix2.Configuration.Scoring.LetterGradeModifiers[PhoenixLetterGrade.AAAPlus]);
        Assert.Equal(MixEnum.Phoenix, doubles.Configuration.Scoring.Mix);
        Assert.Equal(.50, doubles.Configuration.Scoring.LetterGradeModifiers[PhoenixLetterGrade.APlus]);
        Assert.All(boards, b => Assert.True(b.Configuration.IsMom));
        Assert.All(boards, b => Assert.False(b.Configuration.AllowRepeats));
        Assert.All(boards, b => Assert.Equal(TimeSpan.FromMinutes(105), b.Configuration.MaxTime));
        // The frozen config zeroes every other chart type (D15: a board is one type, always).
        Assert.Equal(0, singles.Configuration.Scoring.ChartTypeModifiers[ChartType.Double]);
        Assert.Equal(0, doubles.Configuration.Scoring.ChartTypeModifiers[ChartType.Single]);
    }

    [Fact]
    public async Task CycleStoresOnlyTheSnapshotRowsThatDifferFromTheFolderFloor()
    {
        // §9.3: a chart at folder level + 0.5 gets NO row — the fallback produces the same
        // value. Community scoring levels clamp to [level + 0.5, level + 1.5].
        var noEntry = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var capped = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var mid = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var belowFloor = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();
        var exactFloor = new ChartBuilder().WithLevel(20).WithType(ChartType.Single).Build();

        var mom = new Mock<IMoMRepository>();
        mom.Setup(m => m.GetSeason(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoMSeason?)null);
        IReadOnlyList<MoMBoardSeed>? boards = null;
        mom.Setup(m => m.CreateSeason(It.IsAny<MoMSeason>(), It.IsAny<IReadOnlyList<MoMBoardSeed>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MoMSeason, IReadOnlyList<MoMBoardSeed>, CancellationToken>((_, b, _) => boards = b)
            .Returns(Task.CompletedTask);
        var charts = new Mock<IChartRepository>();
        charts.Setup(r => r.GetCharts(It.IsAny<MixEnum>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { noEntry, capped, mid, belowFloor, exactFloor });
        var scoringLevels = new Mock<IChartScoringLevelRepository>();
        scoringLevels.Setup(s => s.GetScoringLevels(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>
            {
                [capped.Id] = 23.0,      // above level + 1.5 → clamps to 21.5, stored
                [mid.Id] = 21.0,         // inside the band → stored as-is
                [belowFloor.Id] = 20.2,  // below level + 0.5 → floors to 20.5, NOT stored
                [exactFloor.Id] = 20.5   // exactly the floor → NOT stored
            });
        var handler = new MarchOfMurlocsHandler(mom.Object, charts.Object, new Mock<IBus>().Object,
            new Mock<IMessageScheduler>().Object,
            FakeDateTime.At(new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero)).Object,
            scoringLevels.Object);

        await handler.Consume(ContextOf(new CycleMoMCommand()).Object);

        Assert.NotNull(boards);
        var singles = Assert.Single(boards!, b => b.Mix == MixEnum.Phoenix && b.ChartType == ChartType.Single);
        Assert.Equal(2, singles.SnapshotDeltas.Count);
        Assert.Equal(21.5, singles.SnapshotDeltas[capped.Id]);
        Assert.Equal(21.0, singles.SnapshotDeltas[mid.Id]);
        Assert.DoesNotContain(noEntry.Id, singles.SnapshotDeltas.Keys);
        Assert.DoesNotContain(belowFloor.Id, singles.SnapshotDeltas.Keys);
        Assert.DoesNotContain(exactFloor.Id, singles.SnapshotDeltas.Keys);
    }

    [Fact]
    public async Task CyclePrunesEndedEmptySeasonsWhenItCreatesTheNext()
    {
        var mom = new Mock<IMoMRepository>();
        var now = new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero);
        mom.Setup(m => m.GetSeason(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoMSeason?)null);

        var handler = Handler(mom, new Mock<IBus>(), new Mock<IMessageScheduler>(), now);
        await handler.Consume(ContextOf(new CycleMoMCommand()).Object);

        mom.Verify(m => m.PruneEndedEmptySeasons(now, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Every month lands on its own quarter's season. The table this replaces listed
    ///     eleven months and fell through for the twelfth; deriving from now cannot omit one.
    /// </summary>
    [Theory]
    [InlineData(1, 1, "Winter", 3)]
    [InlineData(2, 1, "Winter", 3)]
    [InlineData(3, 1, "Winter", 3)]
    [InlineData(4, 2, "Spring", 6)]
    [InlineData(5, 2, "Spring", 6)]
    [InlineData(6, 2, "Spring", 6)]
    [InlineData(7, 3, "Summer", 9)]
    [InlineData(8, 3, "Summer", 9)]
    [InlineData(9, 3, "Summer", 9)]
    [InlineData(10, 4, "Fall", 12)]
    [InlineData(11, 4, "Fall", 12)]
    [InlineData(12, 4, "Fall", 12)]
    public async Task CycleCreatesTheSeasonOfTheQuarterItRunsIn(int month, int expectedQuarter,
        string expectedName, int expectedEndMonth)
    {
        var (season, _) = await Cycle(new DateTimeOffset(2026, month, 2, 11, 0, 0, TimeSpan.Zero));

        Assert.Equal((byte)expectedQuarter, season.Quarter);
        Assert.Equal($"{expectedName} 2026", season.Name);
        Assert.Equal(expectedEndMonth, season.EndsAt.Month);
        Assert.Equal(2026, season.EndsAt.Year);
    }

    [Fact]
    public async Task CycleNeverCreatesASeasonThatHasAlreadyEnded()
    {
        // The invariant the runaway violated, stated directly: whenever the cycle runs, the
        // season it creates ends ahead of now — there is no history to fall behind.
        foreach (var now in new[]
                 {
                     new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                     new DateTimeOffset(2026, 6, 30, 23, 0, 0, TimeSpan.Zero),
                     new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero),
                     new DateTimeOffset(2027, 12, 31, 12, 0, 0, TimeSpan.Zero)
                 })
        {
            var (season, _) = await Cycle(now);
            Assert.True(season.EndsAt > now,
                $"cycling at {now} produced a season ending {season.EndsAt}");
        }
    }

    [Fact]
    public async Task CycleUsesTheSeasonClockNotUtcAtTheYearBoundary()
    {
        // Midnight UTC on January 1st is still December 31st evening in UTC-5: the cycle must
        // finish out Fall 2026, not skip ahead to Winter 2027 while the season has hours left.
        var (season, _) = await Cycle(new DateTimeOffset(2027, 1, 1, 2, 0, 0, TimeSpan.Zero));

        Assert.Equal("Fall 2026", season.Name);
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(-5)), season.EndsAt);
    }

    [Fact]
    public async Task CycleRollsIntoWinterOnceTheNewYearStartsOnTheSeasonClock()
    {
        var (season, _) = await Cycle(new DateTimeOffset(2027, 1, 1, 11, 0, 0, TimeSpan.Zero));

        Assert.Equal("Winter 2027", season.Name);
        Assert.Equal(new DateTimeOffset(2027, 3, 31, 23, 59, 59, TimeSpan.FromHours(-5)), season.EndsAt);
    }

    [Fact]
    public async Task TryScheduleSeatsTheBoardsALiveSeasonIsMissing()
    {
        // Summer 2026 was created with Phoenix boards only (before D38); the daily run adds the
        // Phoenix 2 pair with a snapshot from that mix's scoring levels, and still schedules
        // the cycle for the season's end.
        var mom = new Mock<IMoMRepository>();
        var bus = new Mock<IBus>();
        var scheduler = new Mock<IMessageScheduler>();
        var endsAt = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(-5));
        var season = Season(2026, 3, endsAt);
        mom.Setup(m => m.GetSeason(2026, 3, It.IsAny<CancellationToken>())).ReturnsAsync(season);
        mom.Setup(m => m.GetBoardKeys(season.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new MoMBoardKey(MixEnum.Phoenix, ChartType.Double), new MoMBoardKey(MixEnum.Phoenix, ChartType.Single) });
        IReadOnlyList<MoMBoardSeed>? added = null;
        mom.Setup(m => m.AddBoards(season.Id, It.IsAny<IReadOnlyList<MoMBoardSeed>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<MoMBoardSeed>, CancellationToken>((_, b, _) => added = b)
            .Returns(Task.CompletedTask);

        var handler = Handler(mom, bus, scheduler, new DateTimeOffset(2026, 9, 6, 11, 0, 0, TimeSpan.Zero));
        await handler.Consume(ContextOf(new TryScheduleMoMCommand()).Object);

        Assert.NotNull(added);
        Assert.Equal(
            new[] { new MoMBoardKey(MixEnum.Phoenix2, ChartType.Double), new MoMBoardKey(MixEnum.Phoenix2, ChartType.Single) },
            added!.Select(b => new MoMBoardKey(b.Mix, b.ChartType)).ToArray());
        Assert.All(added!, b => Assert.Equal(MixEnum.Phoenix2, b.Configuration.Scoring.Mix));
        Assert.All(added!, b => Assert.Equal(season.EndsAt, b.Configuration.EndDate));
        Assert.All(added!, b => Assert.Equal(season.StartsAt, b.Configuration.StartDate));
        Assert.Equal("March of Murlocs Season 2026/3 - Singles (Phoenix 2)",
            (string)added!.Single(b => b.ChartType == ChartType.Single).Configuration.Name);
        mom.Verify(m => m.CreateSeason(It.IsAny<MoMSeason>(), It.IsAny<IReadOnlyList<MoMBoardSeed>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        scheduler.Verify(s => s.SchedulePublish((endsAt + TimeSpan.FromMinutes(1)).UtcDateTime,
            It.IsAny<CycleMoMCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryScheduleAddsNothingToASeasonThatHoldsAllFourBoards()
    {
        var mom = new Mock<IMoMRepository>();
        var endsAt = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(-5));
        mom.Setup(m => m.GetSeason(2026, 3, It.IsAny<CancellationToken>())).ReturnsAsync(Season(2026, 3, endsAt));

        var handler = Handler(mom, new Mock<IBus>(), new Mock<IMessageScheduler>(),
            new DateTimeOffset(2026, 9, 6, 11, 0, 0, TimeSpan.Zero));
        await handler.Consume(ContextOf(new TryScheduleMoMCommand()).Object);

        mom.Verify(m => m.AddBoards(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<MoMBoardSeed>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Runs one CycleMoMCommand with no existing season and returns what it created.</summary>
    private static async Task<(MoMSeason Season, IReadOnlyList<MoMBoardSeed> Boards)> Cycle(
        DateTimeOffset now)
    {
        var mom = new Mock<IMoMRepository>();
        mom.Setup(m => m.GetSeason(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoMSeason?)null);
        MoMSeason? season = null;
        IReadOnlyList<MoMBoardSeed>? boards = null;
        mom.Setup(m => m.CreateSeason(It.IsAny<MoMSeason>(), It.IsAny<IReadOnlyList<MoMBoardSeed>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MoMSeason, IReadOnlyList<MoMBoardSeed>, CancellationToken>((s, b, _) =>
            {
                season = s;
                boards = b;
            })
            .Returns(Task.CompletedTask);

        var handler = Handler(mom, new Mock<IBus>(), new Mock<IMessageScheduler>(), now);
        await handler.Consume(ContextOf(new CycleMoMCommand()).Object);

        Assert.NotNull(season);
        Assert.NotNull(boards);
        return (season!, boards!);
    }
}
