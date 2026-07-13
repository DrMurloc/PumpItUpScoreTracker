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
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RecommendedChartsSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SubmitFeedbackPersistsFeedbackForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var users = new Mock<IFeedbackRepository>();
        var saga = BuildSaga(currentUserId: userId, users: users);
        var feedback = new SuggestionFeedbackRecord(
            SuggestionCategory: Name.From("Push PGs"),
            FeedbackCategory: Name.From("NotInterested"),
            Notes: "Already cleared",
            ShouldHide: true,
            IsPositive: false,
            ChartId: Guid.NewGuid());

        await saga.Handle(new SubmitFeedbackCommand(feedback), CancellationToken.None);

        users.Verify(u => u.SaveFeedback(userId, feedback, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecommendedChartsIncludesSSSPlusButNotPGScoresUnderPushPGs()
    {
        // GetPGPushes selects scores where score is non-null, != 1,000,000 (PG),
        // and LetterGrade == SSSPlus (>= 995,000). This is the only sub-method that
        // is fully deterministic; the rest are guarded by random or empty data.
        var sssPlusChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var pgChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new RecommendedChartsContext()
            .WithCharts(sssPlusChart, pgChart)
            .WithScores(
                Score(sssPlusChart.Id, 999500),     // SSSPlus, not PG → eligible
                Score(pgChart.Id, 1000000));         // PG → excluded

        var result = (await ctx.Saga.Handle(
            new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0),
            CancellationToken.None)).ToArray();

        Assert.Contains(result, r => (string)r.Category == "Push PGs" && r.ChartId == sssPlusChart.Id);
        Assert.DoesNotContain(result, r => r.ChartId == pgChart.Id);
    }

    [Fact]
    public async Task GetRecommendedChartsExcludesPushPGsChartsThatHaveShouldHideFeedback()
    {
        var hiddenChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var visibleChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var userId = Guid.NewGuid();
        var ctx = new RecommendedChartsContext(currentUserId: userId)
            .WithCharts(hiddenChart, visibleChart)
            .WithScores(
                Score(hiddenChart.Id, 999500),
                Score(visibleChart.Id, 999500))
            .WithFeedback(
                new SuggestionFeedbackRecord(Name.From("Push PGs"), Name.From("NotInterested"),
                    Notes: "", ShouldHide: true, IsPositive: false, ChartId: hiddenChart.Id));

        var result = (await ctx.Saga.Handle(
            new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0),
            CancellationToken.None)).ToArray();

        Assert.DoesNotContain(result,
            r => (string)r.Category == "Push PGs" && r.ChartId == hiddenChart.Id);
        Assert.Contains(result,
            r => (string)r.Category == "Push PGs" && r.ChartId == visibleChart.Id);
    }

    [Fact]
    public async Task GetRecommendedChartsClampsCompetitiveLevelToTenWhenStatsAreLower()
    {
        // PlayerStats.CompetitiveLevel = 5 → clamped to 10. GetOldScores then iterates
        // BuildRange(competitive - 2, competitive, 0) = levels 8 and 9. We pin that
        // GetMyRelativeTierListQuery is dispatched for those clamped levels and not
        // for level 3 (which would be 5 - 2 = 3 without the clamp).
        var ctx = new RecommendedChartsContext().WithCompetitiveLevel(5);

        await ctx.Saga.Handle(
            new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0),
            CancellationToken.None);

        ctx.Mediator.Verify(m => m.Send(
            It.Is<GetMyRelativeTierListQuery>(q => (int)q.Level == 8),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        ctx.Mediator.Verify(m => m.Send(
            It.Is<GetMyRelativeTierListQuery>(q => (int)q.Level == 9),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        ctx.Mediator.Verify(m => m.Send(
            It.Is<GetMyRelativeTierListQuery>(q => (int)q.Level == 3),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CategoriesFilterOnlyRunsRequestedBuilders()
    {
        var ctx = new RecommendedChartsContext();

        await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                Categories: new HashSet<RecommendationCategory> { RecommendationCategory.FillScores }),
            CancellationToken.None);

        // Fill Scores runs (it ranks by the approachable-chart tier lists)…
        ctx.Mediator.Verify(m => m.Send(It.IsAny<GetTierListQuery>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        // …while the title- and top-50-backed categories never even fetch their data.
        ctx.Mediator.Verify(m => m.Send(It.IsAny<GetTitleProgressQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ctx.Mediator.Verify(m => m.Send(It.IsAny<GetTop50CompetitiveQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StaticLevelWindowFiltersPushPGsByChartLevel()
    {
        var inWindow = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var below = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var ctx = new RecommendedChartsContext()
            .WithCharts(inWindow, below)
            .WithScores(Score(inWindow.Id, 999500), Score(below.Id, 999500));

        var result = (await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                LevelWindow: RecommendationLevelWindow.Static(18, 22)),
            CancellationToken.None)).ToArray();

        Assert.Contains(result,
            r => (string)r.Category == RecommendationCategories.PushPGs && r.ChartId == inWindow.Id);
        Assert.DoesNotContain(result, r => r.ChartId == below.Id);
    }

    [Fact]
    public async Task DynamicLevelWindowFollowsCompetitiveLevel()
    {
        // CL 18, 1 below / 1 above → window 17–19; the level-20 SSS+ falls outside it.
        var inWindow = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var outside = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new RecommendedChartsContext()
            .WithCompetitiveLevel(18)
            .WithCharts(inWindow, outside)
            .WithScores(Score(inWindow.Id, 999500), Score(outside.Id, 999500));

        var result = (await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                LevelWindow: RecommendationLevelWindow.Dynamic(1, 1)),
            CancellationToken.None)).ToArray();

        Assert.Contains(result,
            r => (string)r.Category == RecommendationCategories.PushPGs && r.ChartId == inWindow.Id);
        Assert.DoesNotContain(result, r => r.ChartId == outside.Id);
    }

    [Fact]
    public async Task DynamicLevelWindowSpreadsAreAsymmetric()
    {
        // 3 below / 0 above at CL 18 → 15–18: relaxed picks below, nothing above.
        var below = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var above = new ChartBuilder().WithType(ChartType.Single).WithLevel(19).Build();
        var ctx = new RecommendedChartsContext()
            .WithCompetitiveLevel(18)
            .WithCharts(below, above)
            .WithScores(Score(below.Id, 999500), Score(above.Id, 999500));

        var result = (await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                LevelWindow: RecommendationLevelWindow.Dynamic(3, 0)),
            CancellationToken.None)).ToArray();

        Assert.Contains(result,
            r => (string)r.Category == RecommendationCategories.PushPGs && r.ChartId == below.Id);
        Assert.DoesNotContain(result, r => r.ChartId == above.Id);
    }

    [Fact]
    public async Task ScoringLevelBasisRatesChartsByCalibratedDifficulty()
    {
        // A printed 15 that scores like a 19.7 belongs in an 18–22 window; a printed 20
        // that scores like a 16.2 does not. Uncalibrated charts fall back to printed level.
        var sandbagged = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var inflated = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new RecommendedChartsContext()
            .WithCharts(sandbagged, inflated)
            .WithScores(Score(sandbagged.Id, 999500), Score(inflated.Id, 999500));
        ctx.Mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>
            {
                [sandbagged.Id] = 19.7,
                [inflated.Id] = 16.2
            });

        var result = (await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                LevelWindow: RecommendationLevelWindow.Static(18, 22, RecommendationLevelBasis.ScoringLevel)),
            CancellationToken.None)).ToArray();

        Assert.Contains(result,
            r => (string)r.Category == RecommendationCategories.PushPGs && r.ChartId == sandbagged.Id);
        Assert.DoesNotContain(result, r => r.ChartId == inflated.Id);
    }

    [Fact]
    public async Task StaticLevelWindowReplacesFillScoresLegacyBand()
    {
        // Legacy fills = CL−3..CL−1 (17–19 at CL 20). A pinned 15–16 window replaces
        // that band outright rather than intersecting it.
        var pinned = new ChartBuilder().WithType(ChartType.Single).WithLevel(16).Build();
        var legacy = new ChartBuilder().WithType(ChartType.Single).WithLevel(18).Build();
        var ctx = new RecommendedChartsContext()
            .WithCompetitiveLevel(20)
            .WithCharts(pinned, legacy);
        ctx.Mediator.Setup(m => m.Send(It.IsAny<GetTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SongTierListEntry("Popularity", pinned.Id, TierListCategory.Easy, 0),
                new SongTierListEntry("Popularity", legacy.Id, TierListCategory.Easy, 0)
            });

        var result = (await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                LevelWindow: RecommendationLevelWindow.Static(15, 16)),
            CancellationToken.None)).ToArray();

        Assert.Contains(result,
            r => (string)r.Category == RecommendationCategories.FillScores && r.ChartId == pinned.Id);
        Assert.DoesNotContain(result,
            r => (string)r.Category == RecommendationCategories.FillScores && r.ChartId == legacy.Id);
    }

    [Fact]
    public async Task StaticLevelWindowReplacesRevisitOldScoresLegacyBand()
    {
        // CL 20 legacy band = CL−2..CL (18–20); a 15–16 window pulls the old level-16
        // score in and drives which relative tier lists get fetched.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(16).Build();
        var ctx = new RecommendedChartsContext()
            .WithCompetitiveLevel(20)
            .WithCharts(chart)
            .WithScores(Score(chart.Id, 950000)); // recorded 2026-01-01 → older than 30 days
        ctx.Mediator.Setup(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new SongTierListEntry("Relative", chart.Id, TierListCategory.Underrated, 0) });

        var result = (await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                LevelWindow: RecommendationLevelWindow.Static(15, 16)),
            CancellationToken.None)).ToArray();

        Assert.Contains(result,
            r => (string)r.Category == RecommendationCategories.RevisitOldScores && r.ChartId == chart.Id);
        ctx.Mediator.Verify(m => m.Send(It.Is<GetMyRelativeTierListQuery>(q => (int)q.Level == 16),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task StaticLevelWindowFiltersImproveTop50()
    {
        var inWindow = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var outside = new ChartBuilder().WithType(ChartType.Single).WithLevel(15).Build();
        var ctx = new RecommendedChartsContext().WithCharts(inWindow, outside);
        ctx.Mediator.Setup(m => m.Send(It.IsAny<GetTop50CompetitiveQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Score(inWindow.Id, 980000), Score(outside.Id, 980000) });

        var result = (await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                LevelWindow: RecommendationLevelWindow.Static(18, 22)),
            CancellationToken.None)).ToArray();

        Assert.Contains(result,
            r => (string)r.Category == RecommendationCategories.ImproveTop50 && r.ChartId == inWindow.Id);
        Assert.DoesNotContain(result,
            r => (string)r.Category == RecommendationCategories.ImproveTop50 && r.ChartId == outside.Id);
    }

    [Fact]
    public async Task PumbilityPushRanksChartsByProjectedGainAndStampsTheGain()
    {
        var big = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).Build();
        var small = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new RecommendedChartsContext()
            .WithCharts(big, small)
            .WithPumbilityGains((small.Id, 7), (big.Id, 42));

        var result = (await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                Categories: new HashSet<RecommendationCategory> { RecommendationCategory.PushPumbility }),
            CancellationToken.None)).ToArray();

        var pushes = result.Where(r => (string)r.Category == RecommendationCategories.PushPumbility).ToArray();
        Assert.Equal(new[] { big.Id, small.Id }, pushes.Select(p => p.ChartId).ToArray());
        Assert.Equal("+42", pushes[0].ChartDetails);
    }

    [Fact]
    public async Task PumbilityPushExcludesHiddenChartsAndNonPositiveGains()
    {
        var gainer = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).Build();
        var hidden = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).Build();
        var zeroGain = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).Build();
        var ctx = new RecommendedChartsContext()
            .WithCharts(gainer, hidden, zeroGain)
            .WithPumbilityGains((gainer.Id, 30), (hidden.Id, 25), (zeroGain.Id, 0))
            .WithFeedback(new SuggestionFeedbackRecord(Name.From(RecommendationCategories.PushPumbility),
                Name.From("NotInterested"), Notes: "", ShouldHide: true, IsPositive: false, ChartId: hidden.Id));

        var result = (await ctx.Saga.Handle(new GetRecommendedChartsQuery(ChartType: null, LevelOffset: 0,
                Categories: new HashSet<RecommendationCategory> { RecommendationCategory.PushPumbility }),
            CancellationToken.None)).ToArray();

        Assert.Contains(result,
            r => (string)r.Category == RecommendationCategories.PushPumbility && r.ChartId == gainer.Id);
        Assert.DoesNotContain(result, r => r.ChartId == hidden.Id);
        Assert.DoesNotContain(result, r => r.ChartId == zeroGain.Id);
    }

    private sealed class RecommendedChartsContext
    {
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<IFeedbackRepository> Users { get; } = new();
        public Mock<IPlayerStatsReader> Stats { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IWeeklyTournamentRepository> Weekly { get; } = new();
        public Mock<IChartListRepository> ChartList { get; } = new();
        public Mock<IRandomNumberGenerator> Random { get; } = new();
        public RecommendedChartsSaga Saga { get; }

        public RecommendedChartsContext(Guid? currentUserId = null)
        {
            var userId = currentUserId ?? Guid.NewGuid();
            CurrentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(userId).Build());
            Stats.Setup(s => s.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>())).ReturnsAsync(ZeroStats(userId));
            // Random.Next(...) returns 0 by default → OrderBy keys are uniform → input order preserved.
            Random.Setup(r => r.Next(It.IsAny<int>())).Returns(0);
            // Default empty for all the mediator queries that random-using sub-methods rely on.
            Mediator.Setup(m => m.Send(It.IsAny<GetTitleProgressQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { OneTitle() });
            Scores.Setup(s => s.GetBestScores(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Chart>());
            Mediator.Setup(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SongTierListEntry>());
            Mediator.Setup(m => m.Send(It.IsAny<GetTierListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SongTierListEntry>());
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50CompetitiveQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<ProjectPumbilityGainsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(EmptyProjection());
            Users.Setup(u => u.GetFeedback(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SuggestionFeedbackRecord>());
            Weekly.Setup(w => w.GetWeeklyCharts(MixEnum.Phoenix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<WeeklyTournamentChart>());

            Saga = new RecommendedChartsSaga(Mediator.Object, CurrentUser.Object, Users.Object, Stats.Object,
                Scores.Object, Weekly.Object, ChartList.Object,
                FakeDateTime.At(Now).Object, Random.Object);
        }

        public RecommendedChartsContext WithCharts(params Chart[] charts)
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
            return this;
        }

        public RecommendedChartsContext WithScores(params RecordedPhoenixScore[] scores)
        {
            Scores.Setup(s => s.GetBestScores(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(scores);
            return this;
        }

        public RecommendedChartsContext WithFeedback(params SuggestionFeedbackRecord[] feedback)
        {
            Users.Setup(u => u.GetFeedback(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(feedback);
            return this;
        }

        public RecommendedChartsContext WithPumbilityGains(params (Guid ChartId, int Gain)[] gains)
        {
            Mediator.Setup(m => m.Send(It.IsAny<ProjectPumbilityGainsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PumbilityProjection(
                    new Dictionary<Guid, PhoenixScore>(),
                    gains.ToDictionary(g => g.ChartId, g => g.Gain),
                    new Dictionary<(ChartType, DifficultyLevel), int>(),
                    new Dictionary<Guid, TierListCategory>(),
                    new Dictionary<Guid, IReadOnlyList<SkillAdjustmentRecord>>()));
            return this;
        }

        public RecommendedChartsContext WithCompetitiveLevel(double level)
        {
            Stats.Setup(s => s.GetStats(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlayerStatsRecord(Guid.NewGuid(), TotalRating: 0, HighestLevel: 1,
                    ClearCount: 0, CoOpRating: 0, CoOpScore: 0, SkillRating: 0, SkillScore: 0,
                    SkillLevel: 0, SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0,
                    DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
                    CompetitiveLevel: level, SinglesCompetitiveLevel: level,
                    DoublesCompetitiveLevel: level));
            return this;
        }
    }

    // GetPushLevels indexes titles[firstAchieved] and crashes on an empty title list,
    // so every test needs at least one PhoenixDifficultyTitle in scope. Use level 28
    // (DifficultyLevel max is 29) and don't put any matching charts in the fixture, so
    // GetPushLevels returns nothing and doesn't pollute the test's assertions.
    private static TitleProgress OneTitle() =>
        new PhoenixTitleProgress(new PhoenixDifficultyTitle(Name.From("Lvl 28 Title"),
            DifficultyLevel.From(28), ratingRequired: 1));

    private static RecordedPhoenixScore Score(Guid chartId, int score) =>
        new(chartId, score, PhoenixPlate.SuperbGame, IsBroken: false,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static PumbilityProjection EmptyProjection() =>
        new(new Dictionary<Guid, PhoenixScore>(), new Dictionary<Guid, int>(),
            new Dictionary<(ChartType, DifficultyLevel), int>(), new Dictionary<Guid, TierListCategory>(),
            new Dictionary<Guid, IReadOnlyList<SkillAdjustmentRecord>>());

    private static PlayerStatsRecord ZeroStats(Guid userId) =>
        new(userId, TotalRating: 0, HighestLevel: 1, ClearCount: 0, CoOpRating: 0, CoOpScore: 0,
            SkillRating: 0, SkillScore: 0, SkillLevel: 0, SinglesRating: 0, SinglesScore: 0,
            SinglesLevel: 0, DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
            CompetitiveLevel: 0, SinglesCompetitiveLevel: 0, DoublesCompetitiveLevel: 0);

    private static RecommendedChartsSaga BuildSaga(
        Guid? currentUserId = null,
        Mock<IMediator>? mediator = null,
        Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IFeedbackRepository>? users = null,
        Mock<IPlayerStatsReader>? stats = null,
        Mock<IScoreReader>? scores = null,
        Mock<IWeeklyTournamentRepository>? weeklyTournament = null,
        Mock<IChartListRepository>? chartList = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null,
        Mock<IRandomNumberGenerator>? random = null)
    {
        currentUser ??= new Mock<ICurrentUserAccessor>();
        var id = currentUserId ?? Guid.NewGuid();
        currentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(id).Build());
        mediator ??= new Mock<IMediator>();
        users ??= new Mock<IFeedbackRepository>();
        stats ??= new Mock<IPlayerStatsReader>();
        scores ??= new Mock<IScoreReader>();
        weeklyTournament ??= new Mock<IWeeklyTournamentRepository>();
        chartList ??= new Mock<IChartListRepository>();
        dateTime ??= FakeDateTime.At(Now);
        random ??= new Mock<IRandomNumberGenerator>();
        return new RecommendedChartsSaga(mediator.Object, currentUser.Object, users.Object, stats.Object,
            scores.Object, weeklyTournament.Object, chartList.Object, dateTime.Object,
            random.Object);
    }
}
