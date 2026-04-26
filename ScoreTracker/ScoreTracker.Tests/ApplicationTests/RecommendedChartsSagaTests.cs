using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.PersonalProgress.Queries;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RecommendedChartsSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleSubmitFeedbackPersistsFeedbackForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        var saga = BuildSaga(currentUserId: userId, users: users);
        var feedback = new SuggestionFeedbackRecord(
            SuggestionCategory: Name.From("Bounties"),
            FeedbackCategory: Name.From("NotInterested"),
            Notes: "Already cleared",
            ShouldHide: true,
            IsPositive: false,
            ChartId: Guid.NewGuid());

        await saga.Handle(new SubmitFeedbackCommand(feedback), CancellationToken.None);

        users.Verify(u => u.SaveFeedback(userId, feedback, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleGetRecommendedChartsIncludesSSSPlusButNotPGScoresUnderPushPGs()
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
    public async Task HandleGetRecommendedChartsExcludesPushPGsChartsThatHaveShouldHideFeedback()
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
    public async Task HandleGetRecommendedChartsClampsCompetitiveLevelToTenWhenStatsAreLower()
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

    private sealed class RecommendedChartsContext
    {
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IPlayerStatsRepository> Stats { get; } = new();
        public Mock<IPhoenixRecordRepository> Scores { get; } = new();
        public Mock<IChartBountyRepository> Bounties { get; } = new();
        public Mock<IWeeklyTournamentRepository> Weekly { get; } = new();
        public Mock<IChartListRepository> ChartList { get; } = new();
        public Mock<IRandomNumberGenerator> Random { get; } = new();
        public RecommendedChartsSaga Saga { get; }

        public RecommendedChartsContext(Guid? currentUserId = null)
        {
            var userId = currentUserId ?? Guid.NewGuid();
            CurrentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(userId).Build());
            Stats.Setup(s => s.GetStats(userId, It.IsAny<CancellationToken>())).ReturnsAsync(ZeroStats(userId));
            // Random.Next(...) returns 0 by default → OrderBy keys are uniform → input order preserved.
            Random.Setup(r => r.Next(It.IsAny<int>())).Returns(0);
            // Default empty for all the mediator queries that random-using sub-methods rely on.
            Mediator.Setup(m => m.Send(It.IsAny<GetTitleProgressQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { OneTitle() });
            Mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Chart>());
            Mediator.Setup(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SongTierListEntry>());
            Mediator.Setup(m => m.Send(It.IsAny<GetTierListQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SongTierListEntry>());
            Mediator.Setup(m => m.Send(It.IsAny<GetTop50CompetitiveQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Mediator.Setup(m => m.Send(It.IsAny<GetChartBountiesQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<ChartBounty>());
            Users.Setup(u => u.GetFeedback(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SuggestionFeedbackRecord>());
            Weekly.Setup(w => w.GetWeeklyCharts(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<WeeklyTournamentChart>());

            Saga = new RecommendedChartsSaga(Mediator.Object, CurrentUser.Object, Users.Object, Stats.Object,
                Scores.Object, Bounties.Object, Weekly.Object, ChartList.Object,
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
            Mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(scores);
            return this;
        }

        public RecommendedChartsContext WithFeedback(params SuggestionFeedbackRecord[] feedback)
        {
            Users.Setup(u => u.GetFeedback(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(feedback);
            return this;
        }

        public RecommendedChartsContext WithCompetitiveLevel(double level)
        {
            Stats.Setup(s => s.GetStats(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
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

    private static PlayerStatsRecord ZeroStats(Guid userId) =>
        new(userId, TotalRating: 0, HighestLevel: 1, ClearCount: 0, CoOpRating: 0, CoOpScore: 0,
            SkillRating: 0, SkillScore: 0, SkillLevel: 0, SinglesRating: 0, SinglesScore: 0,
            SinglesLevel: 0, DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
            CompetitiveLevel: 0, SinglesCompetitiveLevel: 0, DoublesCompetitiveLevel: 0);

    private static RecommendedChartsSaga BuildSaga(
        Guid? currentUserId = null,
        Mock<IMediator>? mediator = null,
        Mock<ICurrentUserAccessor>? currentUser = null,
        Mock<IUserRepository>? users = null,
        Mock<IPlayerStatsRepository>? stats = null,
        Mock<IPhoenixRecordRepository>? scores = null,
        Mock<IChartBountyRepository>? bounties = null,
        Mock<IWeeklyTournamentRepository>? weeklyTournament = null,
        Mock<IChartListRepository>? chartList = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null,
        Mock<IRandomNumberGenerator>? random = null)
    {
        currentUser ??= new Mock<ICurrentUserAccessor>();
        var id = currentUserId ?? Guid.NewGuid();
        currentUser.SetupGet(u => u.User).Returns(new UserBuilder().WithId(id).Build());
        mediator ??= new Mock<IMediator>();
        users ??= new Mock<IUserRepository>();
        stats ??= new Mock<IPlayerStatsRepository>();
        scores ??= new Mock<IPhoenixRecordRepository>();
        bounties ??= new Mock<IChartBountyRepository>();
        weeklyTournament ??= new Mock<IWeeklyTournamentRepository>();
        chartList ??= new Mock<IChartListRepository>();
        dateTime ??= FakeDateTime.At(Now);
        random ??= new Mock<IRandomNumberGenerator>();
        return new RecommendedChartsSaga(mediator.Object, currentUser.Object, users.Object, stats.Object,
            scores.Object, bounties.Object, weeklyTournament.Object, chartList.Object, dateTime.Object,
            random.Object);
    }
}
