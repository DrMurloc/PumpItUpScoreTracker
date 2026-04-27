using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.PersonalProgress.Queries;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PumbilityProjectionSagaTests
{
    [Fact]
    public async Task EmptyTopScoresProducesEmptyProjection()
    {
        var ctx = new ProjectionContext();

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.Empty(result.ExpectedScores);
        Assert.Empty(result.ProjectedGains);
        Assert.Empty(result.InsufficientDataGains);
        Assert.Empty(result.ChartDifficulty);
    }

    [Fact]
    public async Task CohortQueriesIssuedForBothChartTypesWhenTopScoresSpanALevel()
    {
        // One top-50 score on a single-type chart at level 18 means the level range is {18}.
        // The handler should query GetPlayersByCompetitiveRange for both Single and Double anyway —
        // it doesn't gate on whether the user actually has scores of each type.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var ctx = new ProjectionContext()
            .WithCharts(chart)
            .WithTopScore(chart.Id);

        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(
            ChartType.Single, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(
            ChartType.Double, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CohortRecordedScoresQueriedWithLevelBoundsFromTopScores()
    {
        // Top 50 has scores at levels 18 and 20 → expect GetRecordedScores called with min=18, max=20.
        var l18 = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var l20 = new ChartBuilder().WithType(ChartType.Double).WithLevel(20).Build();
        var ctx = new ProjectionContext()
            .WithCharts(l18, l20)
            .WithTopScore(l18.Id)
            .WithTopScore(l20.Id);

        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.PhoenixRecords.Verify(s => s.GetRecordedScores(
            It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<ChartType>(),
            It.Is<DifficultyLevel>(l => (int)l == 18),
            It.Is<DifficultyLevel>(l => (int)l == 20),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ChartDifficultyMirrorsPassCountTierList()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var passCountChartId = Guid.NewGuid();
        var ctx = new ProjectionContext()
            .WithCharts(chart)
            .WithTopScore(chart.Id)
            .WithPassCountTierList(new SongTierListEntry(
                Name.From("Pass Count"), passCountChartId, TierListCategory.Hard, Order: 0));

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.True(result.ChartDifficulty.ContainsKey(passCountChartId));
        Assert.Equal(TierListCategory.Hard, result.ChartDifficulty[passCountChartId]);
    }

    [Fact]
    public async Task SinglesCompetitiveLevelClampedToTenWhenStatsLower()
    {
        // The handler clamps SinglesCompetitiveLevel <= 10 up to 10 for the cohort query.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var ctx = new ProjectionContext()
            .WithCharts(chart)
            .WithTopScore(chart.Id)
            .WithCompetitiveLevels(singles: 5.0, doubles: 5.0);

        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(
            ChartType.Single, 10.0, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(
            ChartType.Double, 10.0, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SinglesCompetitiveLevelPassedThroughWhenAboveTen()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var ctx = new ProjectionContext()
            .WithCharts(chart)
            .WithTopScore(chart.Id)
            .WithCompetitiveLevels(singles: 17.5, doubles: 16.0);

        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(
            ChartType.Single, 17.5, 1, It.IsAny<CancellationToken>()), Times.Once);
        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(
            ChartType.Double, 16.0, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoCohortDataProducesEmptyExpectedAndGains()
    {
        // Top score exists but cohort returns nothing → no chartAverages, no expected scores.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var ctx = new ProjectionContext()
            .WithCharts(chart)
            .WithTopScore(chart.Id);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.Empty(result.ExpectedScores);
        Assert.Empty(result.ProjectedGains);
    }

    private sealed class ProjectionContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IPlayerStatsRepository> Stats { get; } = new();
        public Mock<IPhoenixRecordRepository> PhoenixRecords { get; } = new();
        public PumbilityProjectionSaga Saga { get; }

        private readonly List<Chart> _charts = new();
        private readonly List<RecordedPhoenixScore> _topScores = new();
        private readonly List<RecordedPhoenixScore> _allUserScores = new();
        private readonly List<SongTierListEntry> _passCountTierList = new();
        private double _singlesCompetitive = 17.0;
        private double _doublesCompetitive = 17.0;

        public ProjectionContext()
        {
            Stats.Setup(s => s.GetStats(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new PlayerStatsRecord(UserId,
                    TotalRating: 0, HighestLevel: 1, ClearCount: 0, CoOpRating: 0, CoOpScore: 0,
                    SkillRating: 0, SkillScore: 0, SkillLevel: 0,
                    SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0,
                    DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
                    CompetitiveLevel: (_singlesCompetitive + _doublesCompetitive) / 2,
                    SinglesCompetitiveLevel: _singlesCompetitive,
                    DoublesCompetitiveLevel: _doublesCompetitive));

            Stats.Setup(s => s.GetPlayersByCompetitiveRange(
                    It.IsAny<ChartType?>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Guid>());

            PhoenixRecords.Setup(s => s.GetRecordedScores(
                    It.IsAny<IEnumerable<Guid>>(), It.IsAny<ChartType>(),
                    It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());

            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _charts.AsEnumerable());
            Mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _allUserScores.AsEnumerable());
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _topScores.AsEnumerable());
            Mediator.Setup(m => m.Send(It.IsAny<GetTierListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _passCountTierList.AsEnumerable());

            Saga = new PumbilityProjectionSaga(Mediator.Object, Stats.Object, PhoenixRecords.Object);
        }

        public ProjectionContext WithCharts(params Chart[] charts)
        {
            _charts.AddRange(charts);
            return this;
        }

        public ProjectionContext WithTopScore(Guid chartId, int score = 950000)
        {
            _topScores.Add(new RecordedPhoenixScore(chartId, score, PhoenixPlate.SuperbGame,
                IsBroken: false, RecordedDate: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            return this;
        }

        public ProjectionContext WithPassCountTierList(params SongTierListEntry[] entries)
        {
            _passCountTierList.AddRange(entries);
            return this;
        }

        public ProjectionContext WithCompetitiveLevels(double singles, double doubles)
        {
            _singlesCompetitive = singles;
            _doublesCompetitive = doubles;
            return this;
        }
    }
}
