using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TitleSagaTests
{
    [Fact]
    public async Task TitleProgressForPhoenix2IsEmptyAndDoesNotThrow()
    {
        // Locked decision: Phoenix 2 launches with an EMPTY title list (including
        // difficulty titles) — progress must be empty, never a Phoenix fallthrough.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix2, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));

        var progress = await ctx.Saga.Handle(new GetTitleProgressQuery(MixEnum.Phoenix2), CancellationToken.None);

        Assert.Empty(progress);
    }

    [Fact]
    public async Task TitleProgressForPhoenixStillBuildsFromThePhoenixList()
    {
        // Contrast case: the same setup under Phoenix produces the real (non-empty) list.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));

        var progress = await ctx.Saga.Handle(new GetTitleProgressQuery(MixEnum.Phoenix), CancellationToken.None);

        Assert.NotEmpty(progress);
    }

    [Fact]
    public async Task TitleProgressForAnUnknownMixThrowsInsteadOfFallingBackToPhoenix()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ctx.Saga.Handle(new GetTitleProgressQuery((MixEnum)999), CancellationToken.None));
    }

    [Fact]
    public async Task Phoenix2ScoreEventProducesZeroTitlesAndNoAcquisitionEvent()
    {
        // The commit-5 "ignore non-Phoenix" guard is gone: a Phoenix 2 score event flows
        // through title processing and simply completes nothing.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix2, chart);
        ctx.GivenBestScores(Score(chart.Id, 999000));

        await ctx.Saga.Consume(BuildContext(PlayerScoresUpdatedEvent.Create(
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), ctx.UserId, MixEnum.Phoenix2,
            new[] { new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, true, null, 999000, "SuperbGame", false) })));

        ctx.Titles.Verify(t => t.SaveTitles(MixEnum.Phoenix2, ctx.UserId,
            It.Is<IEnumerable<TitleAchievedRecord>>(titles => !titles.Any()),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Titles.Verify(t => t.SetHighestDifficultyTitle(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<ScoreTracker.SharedKernel.ValueTypes.Name>(),
            It.IsAny<ScoreTracker.SharedKernel.ValueTypes.DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class SagaContext
    {
        private readonly MixEnum _mix;
        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<ITitleRepository> Titles { get; } = new();
        public Mock<IBus> Bus { get; } = new();
        public TitleSaga Saga { get; }

        public SagaContext(MixEnum mix, params Chart[] charts)
        {
            _mix = mix;
            CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
            CurrentUser.SetupGet(c => c.User).Returns(new UserBuilder().WithId(UserId).Build());
            Charts.Setup(c => c.GetCharts(mix, It.IsAny<ScoreTracker.SharedKernel.ValueTypes.DifficultyLevel?>(),
                    It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
            Titles.Setup(t => t.GetCompletedTitles(mix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<TitleAchievedRecord>());
            Scores.Setup(s => s.GetBestScores(mix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());

            Saga = new TitleSaga(CurrentUser.Object, Scores.Object, Charts.Object, Titles.Object, Bus.Object);
        }

        public void GivenBestScores(params RecordedPhoenixScore[] scores)
        {
            Scores.Setup(s => s.GetBestScores(_mix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(scores);
        }
    }

    private static RecordedPhoenixScore Score(Guid chartId, int score) =>
        new(chartId, score, PhoenixPlate.SuperbGame, false,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
