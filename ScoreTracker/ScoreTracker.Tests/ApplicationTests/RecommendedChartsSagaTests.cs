using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

// GetRecommendedChartsQuery is not covered: the handler instantiates `new Random()`
// in five different sub-methods (GetOldScores, GetRandomFromTop50Charts, GetPassFills,
// GetBounties, GetPushLevels) and dispatches to ten+ mediator queries to assemble the
// final list. Without an IRandomNumberGenerator-style port and a way to substitute
// the inner queries, any assertion would either be brittle (snapshot-y) or
// re-implement the saga's filter logic. Flagging for a refactor pass — same shape
// as the WeeklyTournamentSaga.Consume(UpdateWeeklyChartsEvent) gap called out in
// the priority-2 PR.
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
        Mock<IDateTimeOffsetAccessor>? dateTime = null)
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
        return new RecommendedChartsSaga(mediator.Object, currentUser.Object, users.Object, stats.Object,
            scores.Object, bounties.Object, weeklyTournament.Object, chartList.Object, dateTime.Object);
    }
}
