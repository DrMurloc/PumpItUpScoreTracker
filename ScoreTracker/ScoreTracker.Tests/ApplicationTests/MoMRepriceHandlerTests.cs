using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class MoMRepriceHandlerTests
{
    private static readonly Guid SourceBoard = Guid.NewGuid();
    private static readonly Guid TargetBoard = Guid.NewGuid();
    private static readonly Guid SeasonA = Guid.NewGuid();
    private static readonly Guid SeasonB = Guid.NewGuid();

    [Fact]
    public async Task IsolatesTheSnapshotSwapAndTheTableRecutAgainstTheOriginal()
    {
        // Neutral modifiers make the price exactly the level rating: the chart sits at
        // folder 20 with no old snapshot row (prices as L20), the new season re-rates it to
        // 21.5 (prices as L21). Old tables: L20=1000/L21=1100; new: L20=2000/L21=2200.
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        var context = new Context(chart)
            .WithSessionCharts(Row(chart.Id, 0))
            .WithStoredTotal(1000)
            .WithTables(SourceBoard, l20: 1000, l21: 1100)
            .WithTables(TargetBoard, l20: 2000, l21: 2200)
            .WithSnapshot(SourceBoard)
            .WithSnapshot(TargetBoard, (chart.Id, 21.5));

        var split = await context.Handle(new RepriceMoMSessionQuery(context.SessionId, TargetBoard));

        Assert.NotNull(split);
        Assert.Equal(1000, split!.OriginalTotal);
        // Snapshot swapped alone: old tables price the re-rated 21.5 as L21 = 1100.
        Assert.Equal(100, split.ChartReratingDelta);
        // Tables swapped alone: new tables price the un-re-rated 20.5 as L20 = 2000.
        Assert.Equal(1000, split.TableRecutDelta);
        // The full target configuration: new tables at 21.5 = 2200. The deltas multiply —
        // 100 + 1000 < 1200 — which is the shape the doc says to present.
        Assert.Equal(2200, split.RepricedTotal);
        Assert.Equal(1, split.ChartsReratedCount);
        Assert.Equal(2200, split.RepricedChartPoints[chart.Id]);
    }

    [Fact]
    public async Task PerChartPricesFloorBeforeTheSumLikeTheLiveScoring()
    {
        // A 21.75 override prices between L21 and L22 — 1100 + 0.25 × 100 = 1125 exactly,
        // and two of them must land as 1125 + 1125, never round up through the sum.
        var chartA = new ChartBuilder().WithLevel(21).WithType(ChartType.Double)
            .WithSongName("A").Build();
        var chartB = new ChartBuilder().WithLevel(21).WithType(ChartType.Double)
            .WithSongName("B").Build();
        var context = new Context(chartA, chartB)
            .WithSessionCharts(Row(chartA.Id, 0), Row(chartB.Id, 1))
            .WithStoredTotal(2000)
            .WithTables(SourceBoard, l20: 1000, l21: 1000)
            .WithTables(TargetBoard, l20: 1000, l21: 1100)
            .WithSnapshot(SourceBoard)
            .WithSnapshot(TargetBoard, (chartA.Id, 21.75), (chartB.Id, 21.75));

        var split = await context.Handle(new RepriceMoMSessionQuery(context.SessionId, TargetBoard));

        Assert.Equal(2250, split!.RepricedTotal);
        Assert.Equal(1125, split.RepricedChartPoints[chartA.Id]);
    }

    [Fact]
    public async Task RefusesAcrossChartTypesAndForDraftsAndUnknownBoards()
    {
        var chart = new ChartBuilder().WithLevel(20).WithType(ChartType.Double).Build();
        var context = new Context(chart)
            .WithSessionCharts(Row(chart.Id, 0))
            .WithStoredTotal(1000)
            .WithTables(SourceBoard, 1000, 1100)
            .WithTables(TargetBoard, 2000, 2200)
            .WithSnapshot(SourceBoard)
            .WithSnapshot(TargetBoard);

        // A Singles target is a different sport (D15).
        context.MakeTarget(ChartType.Single);
        Assert.Null(await context.Handle(new RepriceMoMSessionQuery(context.SessionId, TargetBoard)));

        context.MakeTarget(ChartType.Double);
        Assert.NotNull(await context.Handle(new RepriceMoMSessionQuery(context.SessionId, TargetBoard)));

        context.MakeDraft();
        Assert.Null(await context.Handle(new RepriceMoMSessionQuery(context.SessionId, TargetBoard)));

        Assert.Null(await context.Handle(new RepriceMoMSessionQuery(Guid.NewGuid(), TargetBoard)));
    }

    private static MoMSessionChartRecord Row(Guid chartId, int ordinal)
    {
        // 990,000 is SSS on the neutral table — every modifier 1.0, so the price is the
        // level rating alone.
        return new MoMSessionChartRecord(ordinal, chartId, 990000, "SuperbGame", false, 0, 0, null);
    }

    private sealed class Context
    {
        private readonly Mock<IChartRepository> _charts = new();
        private readonly Mock<IMoMRepository> _mom = new();
        private MoMBoardRecord _target = new(TargetBoard, SeasonB, MixEnum.Phoenix, ChartType.Double);
        private MoMSessionRecord _session;

        public Context(params Chart[] charts)
        {
            SessionId = Guid.NewGuid();
            _session = new MoMSessionRecord(SessionId, SourceBoard, Guid.NewGuid(),
                DateTimeOffset.UtcNow, 0, charts.Length, 0, 20.5, 12, 20, 21, null);
            _mom.Setup(m => m.GetSession(SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _session);
            _mom.Setup(m => m.GetBoards(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new[]
                {
                    new MoMBoardRecord(SourceBoard, SeasonA, MixEnum.Phoenix, ChartType.Double),
                    _target
                });
            _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null,
                    It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
        }

        public Guid SessionId { get; }

        public Context WithStoredTotal(int total)
        {
            _session = _session with { TotalScore = total };
            return this;
        }

        public Context WithSessionCharts(params MoMSessionChartRecord[] rows)
        {
            _mom.Setup(m => m.GetSessionCharts(SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(rows);
            return this;
        }

        public Context WithTables(Guid boardId, int l20, int l21)
        {
            _mom.Setup(m => m.GetBoardConfiguration(boardId, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Configuration(boardId, l20, l21));
            return this;
        }

        public Context WithSnapshot(Guid boardId, params (Guid ChartId, double Level)[] deltas)
        {
            _mom.Setup(m => m.GetSeasonSnapshot(boardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deltas.ToDictionary(d => d.ChartId, d => d.Level));
            return this;
        }

        public void MakeTarget(ChartType type)
        {
            _target = new MoMBoardRecord(TargetBoard, SeasonB, MixEnum.Phoenix, type);
        }

        public void MakeDraft()
        {
            _session = _session with { PublishedAt = null };
        }

        public Task<MoMSessionReprice?> Handle(RepriceMoMSessionQuery query)
        {
            return new MoMRepriceHandler(_mom.Object, _charts.Object)
                .Handle(query, CancellationToken.None);
        }

        private static TournamentConfiguration Configuration(Guid boardId, int l20, int l21)
        {
            var scoring = new ScoringConfiguration
            {
                AdjustToTime = false,
                ContinuousLetterGradeScale = false,
                StageBreakModifier = 1.0
            };
            foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
                scoring.LetterGradeModifiers[grade] = 1.0;
            foreach (var plate in Enum.GetValues<PhoenixPlate>())
                scoring.PlateModifiers[plate] = 1.0;
            scoring.LevelRatings[DifficultyLevel.From(20)] = l20;
            scoring.LevelRatings[DifficultyLevel.From(21)] = l21;
            scoring.LevelRatings[DifficultyLevel.From(22)] = l21 + 100;
            return new TournamentConfiguration(boardId, "Board", scoring, false, true)
            {
                MaxTime = TimeSpan.FromMinutes(105)
            };
        }
    }
}
