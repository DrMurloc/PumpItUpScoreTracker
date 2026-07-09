using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
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
    public async Task Phoenix2ScoreCaptureProducesZeroTitlesAndNoAcquisitionEvent()
    {
        // The commit-5 "ignore non-Phoenix" guard is gone: a Phoenix 2 batch flows
        // through the title step and simply completes nothing (its list is empty).
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix2, chart);
        ctx.GivenBestScores(Score(chart.Id, 999000));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix2, null,
                new[] { new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, true, null, 999000, "SuperbGame", false) }),
            CancellationToken.None);

        ctx.Titles.Verify(t => t.SaveTitles(MixEnum.Phoenix2, ctx.UserId,
            It.Is<IEnumerable<TitleAchievedRecord>>(titles => !titles.Any()),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Titles.Verify(t => t.SetHighestDifficultyTitle(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<ScoreTracker.SharedKernel.ValueTypes.Name>(),
            It.IsAny<ScoreTracker.SharedKernel.ValueTypes.DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(result.Progress);
    }

    [Fact]
    public async Task CaptureSuppressesTheLegacyAnnouncementTheCardNowCarries()
    {
        // Score-driven completions ride the snapshot card; the legacy Discord message must
        // NOT also fire (it survives only on the detected-titles path). Another Truth S6 is
        // the [The 1st] boss breaker — passing it crosses the title incomplete → done, which
        // the batch-crossing detects even though the site path may have saved it first.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(6)
            .WithSongName("Another Truth").Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix, null,
                new[]
                {
                    new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, true, null, 950000, "SuperbGame", false)
                }),
            CancellationToken.None);

        Assert.Contains(result.Milestones, m => m.Kind == MilestoneKind.TitleCompleted);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProgressDeltasReportTitlesThatMovedTowardCompletion()
    {
        // Real per-title deltas (owner call): the before-state reconstructs from the
        // change's old score, and only titles whose rounded percent moved make the list.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);
        ctx.GivenBestScores(Score(chart.Id, 999000));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix, null,
                new[]
                {
                    new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, false, 500000, 999000, "SuperbGame", false)
                }),
            CancellationToken.None);

        Assert.NotEmpty(result.Progress);
        Assert.All(result.Progress, d => Assert.True(d.NewPercent > d.OldPercent));
        Assert.True(result.Progress.Count <= 5);
    }

    private sealed class SagaContext
    {
        private readonly MixEnum _mix;
        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<ICurrentUserAccessor> CurrentUser { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<ITitleRepository> Titles { get; } = new();
        public Mock<IPlayerMilestoneRepository> Milestones { get; } = new();
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

            Saga = new TitleSaga(CurrentUser.Object, Scores.Object, Charts.Object, Titles.Object,
                Milestones.Object, FakeDateTime.At(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero)).Object,
                Bus.Object);
        }

        public void GivenBestScores(params RecordedPhoenixScore[] scores)
        {
            Scores.Setup(s => s.GetBestScores(_mix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(scores);
        }
    }

    [Fact]
    public async Task DetectedBasicBadgesAreCapturedAsMilestonesAndAnnounced()
    {
        // Site-only badges (CompletionRequired == 0: events, play/plate counts) have no
        // session and no card — the legacy announcement stays alive on this path only.
        var ctx = new SagaContext(MixEnum.Phoenix);

        await ctx.Saga.Consume(BuildContext(new TitlesDetectedEvent(ctx.UserId,
            new[] { "RISE CHALLENGER" }, MixEnum.Phoenix)));

        ctx.Milestones.Verify(m => m.Append(MixEnum.Phoenix, ctx.UserId,
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w => w.Any(x =>
                x.Kind == MilestoneKind.TitleCompleted && x.Title == "RISE CHALLENGER")),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DetectedComputableTitlesAreSavedButLeftToTheScorePath()
    {
        // A difficulty title the site reports (CompletionRequired > 0) is score-computable,
        // so the site path saves it (DB stays correct) but does NOT announce or milestone it —
        // the session card carries it via the score path instead.
        var ctx = new SagaContext(MixEnum.Phoenix);

        await ctx.Saga.Consume(BuildContext(new TitlesDetectedEvent(ctx.UserId,
            new[] { "Intermediate Lv. 1" }, MixEnum.Phoenix)));

        ctx.Titles.Verify(t => t.SaveTitles(MixEnum.Phoenix, ctx.UserId,
            It.Is<IEnumerable<TitleAchievedRecord>>(titles =>
                titles.Any(x => x.Title.ToString() == "Intermediate Lv. 1")),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ctx.Milestones.Verify(m => m.Append(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<IEnumerable<PlayerMilestoneWrite>>(), It.IsAny<CancellationToken>()), Times.Never);
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
