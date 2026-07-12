using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.PlayerProgress.Contracts.Queries;
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
            MixEnum.Phoenix, ChartType.Single, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(
            MixEnum.Phoenix, ChartType.Double, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
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

        ctx.PhoenixRecords.Verify(s => s.GetScores(
            MixEnum.Phoenix,
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
            MixEnum.Phoenix, ChartType.Single, 10.0, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(
            MixEnum.Phoenix, ChartType.Double, 10.0, It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
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
            MixEnum.Phoenix, ChartType.Single, 17.5, 1, It.IsAny<CancellationToken>()), Times.Once);
        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(
            MixEnum.Phoenix, ChartType.Double, 16.0, 1, It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task PhoenixRanksOneMixedPool()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var ctx = new ProjectionContext().WithCharts(chart).WithTopScore(chart.Id);

        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        ctx.Mediator.Verify(m => m.Send(It.Is<GetTop50ForPlayerQuery>(q => q.ChartType == null),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(It.Is<GetTop50ForPlayerQuery>(q => q.ChartType != null),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Phoenix2RanksIndependentSinglesAndDoublesPools()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var ctx = new ProjectionContext().WithCharts(chart).WithTopScore(chart.Id);

        await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        ctx.Mediator.Verify(m => m.Send(It.Is<GetTop50ForPlayerQuery>(q => q.ChartType == ChartType.Single),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(It.Is<GetTop50ForPlayerQuery>(q => q.ChartType == ChartType.Double),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Mediator.Verify(m => m.Send(It.Is<GetTop50ForPlayerQuery>(q => q.ChartType == null),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExpectedScoreInterpolatesCohortDistributionAtMyPercentile()
    {
        // My 950k on c1 sits at index 1 of the cohort's descending c1 scores
        // [960, 950, 940, 930]k → percentile (1/4)·0.95 = 0.2375. On c2's distribution
        // [970, 955, 945, 935]k the target (0.95) interpolates between index 0 and 1:
        // 955,000 + (970,000 − 955,000)·(1 − 0.95) = 955,750.
        var c1 = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var c2 = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var ctx = new ProjectionContext()
            .WithCharts(c1, c2)
            .WithTopScore(c1.Id, 950_000)
            .WithBestScore(c1.Id, 950_000)
            .WithCohortUser()
            .WithCohortScores(ChartType.Single, c1.Id, 960_000, 950_000, 940_000, 930_000)
            .WithCohortScores(ChartType.Single, c2.Id, 970_000, 955_000, 945_000, 935_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId), CancellationToken.None);

        Assert.Equal(955_750, (int)result.ExpectedScores[c2.Id]);
    }

    [Fact]
    public async Task Phoenix2GainsMeasureAgainstTheChartsOwnPool()
    {
        // Singles pool is FULL (50 rated charts) → its baseline is the lowest of those
        // ratings; the doubles pool has one score → underfilled, baseline 0. The same
        // expected score therefore yields a full-contribution gain on a doubles chart
        // and a baseline-reduced gain on a singles chart.
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var poolCharts = Enumerable.Range(0, 50)
            .Select(_ => new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build())
            .ToArray();
        var doublesChart = new ChartBuilder().WithType(ChartType.Double).WithLevel(18).Build();
        var singleCandidate = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var doubleCandidate = new ChartBuilder().WithType(ChartType.Double).WithLevel(18).Build();

        var ctx = new ProjectionContext()
            .WithCharts(poolCharts.Concat(new[] { doublesChart, singleCandidate, doubleCandidate }).ToArray())
            .WithTopScore(doublesChart.Id, 951_000)
            .WithCohortUser()
            .WithCohortScores(ChartType.Single, singleCandidate.Id, 992_000, 991_000, 990_000, 989_000)
            .WithCohortScores(ChartType.Double, doubleCandidate.Id, 970_000, 960_000, 950_000, 940_000);
        foreach (var poolChart in poolCharts) ctx.WithTopScore(poolChart.Id, 951_000);

        var result = await ctx.Saga.Handle(new ProjectPumbilityGainsQuery(ctx.UserId, MixEnum.Phoenix2),
            CancellationToken.None);

        // No cohort data on my played charts → percentile defaults to 0.5, which lands
        // exactly on sorted[2] for both candidates.
        Assert.Equal(990_000, (int)result.ExpectedScores[singleCandidate.Id]);
        Assert.Equal(950_000, (int)result.ExpectedScores[doubleCandidate.Id]);

        // Projected scores ride the empirical plate curve (pinned in
        // ExpectedPlateForScoreTests): 990k → Marvelous Game, 950k → Fair Game. The
        // doubles gain is full contribution — its pool is underfilled, baseline 0.
        var singlesBaseline = (int)scoring.GetScore(poolCharts[0], 951_000, PhoenixPlate.SuperbGame, false);
        var expectedSingleGain = (int)(scoring.GetScore(singleCandidate, 990_000, PhoenixPlate.MarvelousGame, false)
                                       - singlesBaseline);
        var expectedDoubleGain = (int)scoring.GetScore(doubleCandidate, 950_000, PhoenixPlate.FairGame, false);
        Assert.Equal(expectedSingleGain, result.ProjectedGains[singleCandidate.Id]);
        Assert.Equal(expectedDoubleGain, result.ProjectedGains[doubleCandidate.Id]);
    }

    private sealed class ProjectionContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IPlayerStatsReader> Stats { get; } = new();
        public Mock<IScoreReader> PhoenixRecords { get; } = new();
        public PumbilityProjectionSaga Saga { get; }

        private readonly List<Chart> _charts = new();
        private readonly List<RecordedPhoenixScore> _topScores = new();
        private readonly List<RecordedPhoenixScore> _allUserScores = new();
        private readonly List<SongTierListEntry> _passCountTierList = new();
        private double _singlesCompetitive = 17.0;
        private double _doublesCompetitive = 17.0;

        private readonly List<Guid> _cohortUsers = new();
        private readonly List<(ChartType Type, RecordedPhoenixScore Score)> _cohortScores = new();

        public ProjectionContext()
        {
            Stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new PlayerStatsRecord(UserId,
                    TotalRating: 0, HighestLevel: 1, ClearCount: 0, CoOpRating: 0, CoOpScore: 0,
                    SkillRating: 0, SkillScore: 0, SkillLevel: 0,
                    SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0,
                    DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
                    CompetitiveLevel: (_singlesCompetitive + _doublesCompetitive) / 2,
                    SinglesCompetitiveLevel: _singlesCompetitive,
                    DoublesCompetitiveLevel: _doublesCompetitive));

            Stats.Setup(s => s.GetPlayersByCompetitiveRange(
                    It.IsAny<MixEnum>(), It.IsAny<ChartType?>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _cohortUsers.AsEnumerable());

            PhoenixRecords.Setup(s => s.GetScores(
                    It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<ChartType>(),
                    It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, IEnumerable<Guid> _, ChartType type, DifficultyLevel _,
                        DifficultyLevel _, CancellationToken _) =>
                    _cohortScores.Where(cs => cs.Type == type).Select(cs => cs.Score).ToArray());

            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _charts.AsEnumerable());
            PhoenixRecords.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _allUserScores.AsEnumerable());
            // Per-type filtering mirrors the real handler: ChartType == null is the mixed
            // pool; a typed query only returns that type's charts.
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50ForPlayerQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRequest<IEnumerable<RecordedPhoenixScore>> request, CancellationToken _) =>
                {
                    var query = (GetTop50ForPlayerQuery)request;
                    return _topScores.Where(ts => query.ChartType == null ||
                                                  _charts.First(c => c.Id == ts.ChartId).Type == query.ChartType);
                });
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

        public ProjectionContext WithBestScore(Guid chartId, int score)
        {
            _allUserScores.Add(new RecordedPhoenixScore(chartId, score, PhoenixPlate.SuperbGame,
                IsBroken: false, RecordedDate: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            return this;
        }

        public ProjectionContext WithCohortUser()
        {
            _cohortUsers.Add(Guid.NewGuid());
            return this;
        }

        public ProjectionContext WithCohortScores(ChartType type, Guid chartId, params int[] scores)
        {
            _cohortScores.AddRange(scores.Select(s => (type,
                new RecordedPhoenixScore(chartId, s, PhoenixPlate.SuperbGame, IsBroken: false,
                    RecordedDate: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)))));
            return this;
        }
    }
}
