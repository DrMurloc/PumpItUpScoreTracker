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
    public async Task TitleProgressForPhoenix2BuildsFromThePhoenix2List()
    {
        // The real Phoenix 2 catalog (crawled from the live title.php) — its own list,
        // never a Phoenix fallthrough.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();
        var ctx = new SagaContext(MixEnum.Phoenix2, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));

        var progress =
            (await ctx.Saga.Handle(new GetTitleProgressQuery(MixEnum.Phoenix2), CancellationToken.None)).ToArray();

        Assert.Contains(progress, p => p.Title.Name == "[S] INTERMEDIATE LV.1");
        Assert.DoesNotContain(progress, p => p.Title.Name == "Intermediate Lv. 1"); // Phoenix-only
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
    public async Task Phoenix2ScoreCaptureBelowEveryThresholdCompletesNothingButReportsPoolProgress()
    {
        // One 999k on an unremarkable L20 single: no P2 title completes (the pool value is
        // far below the 5000+ ladder floor and the chart matches no skill/boss title), no
        // legacy event fires, and highest-difficulty stays untouched (P2's ladder isn't
        // level-keyed). The PUMBILITY ladder still MOVED, so progress deltas report it.
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
        Assert.Contains(result.Progress, d => d.Title == "[S] INTERMEDIATE LV.1");
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

    [Fact]
    public async Task ContiguousLadderFloorsKeepAllButTheActiveRungOffTheProgressList()
    {
        // The reported bug: one single score reported progress on EVERY [S] pumbility rung at
        // once ("[S] INTERMEDIATE LV.1 0% → 19%, LV.2 0% → 16%, LV.3 0% → 13%") because the delta
        // percent divided by the raw requirement, ignoring the floor. Ladder floors are
        // contiguous (a rung floors on the rung below's requirement), so at most one rung can be
        // mid-progress — no ladder may report two rungs moving at once.
        var single = new ChartBuilder().WithType(ChartType.Single).WithLevel(23).Build();
        var ctx = new SagaContext(MixEnum.Phoenix2, single);
        ctx.GivenBestScores(Score(single.Id, 985000));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix2, null,
                new[]
                {
                    new PlayerScoresUpdatedEvent.ScoreChange(single.Id, false, 500000, 985000, "SuperbGame", false)
                }),
            CancellationToken.None);

        Assert.Contains(result.Progress, d => d.Title.StartsWith("[S]")); // the [S] ladder is exercised
        var doubledUp = result.Progress
            .GroupBy(LadderBase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} -> {string.Join(", ", g.Select(d => d.Title))}")
            .ToArray();
        Assert.True(doubledUp.Length == 0,
            "A ladder reported multiple rungs moving at once (floor ignored): " + string.Join(" | ", doubledUp));
    }

    // Strip a trailing "LV.N" / "Lv. N" rung number so sibling rungs collapse to one ladder key.
    private static string LadderBase(TitleProgressDelta delta) =>
        System.Text.RegularExpressions.Regex.Replace(delta.Title, @"\s*[Ll][Vv]\.?\s*\d+$", "").Trim();

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

        public void GivenSessionMilestones(Guid sessionId, params PlayerMilestoneRecord[] milestones)
        {
            Milestones.Setup(m => m.GetMilestonesBySessions(It.IsAny<Guid>(),
                    It.Is<IEnumerable<Guid>>(ids => ids.Contains(sessionId)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(milestones);
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

    [Fact]
    public async Task DetectedBadgesRideTheSessionWhenTheImportSavedScores()
    {
        // With a SessionId (the import saved scores), a site-only badge is attributed to that
        // session and the legacy announcement is suppressed — the snapshot card carries it.
        var sessionId = Guid.NewGuid();
        var ctx = new SagaContext(MixEnum.Phoenix);

        await ctx.Saga.Consume(BuildContext(new TitlesDetectedEvent(ctx.UserId,
            new[] { "RISE CHALLENGER" }, MixEnum.Phoenix, sessionId)));

        ctx.Milestones.Verify(m => m.Append(MixEnum.Phoenix, ctx.UserId,
            It.Is<IEnumerable<PlayerMilestoneWrite>>(w => w.Any(x =>
                x.Kind == MilestoneKind.TitleCompleted && x.Title == "RISE CHALLENGER"
                && x.SessionId == sessionId)),
            It.IsAny<CancellationToken>()), Times.Once);
        ctx.Bus.Verify(b => b.Publish(It.IsAny<NewTitlesAcquiredEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CaptureSingleSourcesTheSessionsTitleMilestonesFoldingInSiteBadges()
    {
        // The card shows ALL of a session's completions: with a SessionId, CaptureSessionTitles
        // single-sources from the milestone table, folding the site path's basic badges and
        // paragon gains in with the score-crossing ones — and dropping non-title milestones.
        var when = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var sessionId = Guid.NewGuid();
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(6)
            .WithSongName("Another Truth").Build();
        var ctx = new SagaContext(MixEnum.Phoenix, chart);
        ctx.GivenBestScores(Score(chart.Id, 950000));
        ctx.GivenSessionMilestones(sessionId,
            new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, sessionId, when, null, null,
                "RISE CHALLENGER", null),
            new PlayerMilestoneRecord(MilestoneKind.ParagonLevelGain, sessionId, when, null, null,
                "Expert Lv. 2", "PG"),
            new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, sessionId, when, 1, 2, null, null));

        var result = await ctx.Saga.Handle(new TitleSaga.CaptureSessionTitles(ctx.UserId, MixEnum.Phoenix,
                sessionId,
                new[]
                {
                    new PlayerScoresUpdatedEvent.ScoreChange(chart.Id, true, null, 950000, "SuperbGame", false)
                }),
            CancellationToken.None);

        Assert.Contains(result.Milestones,
            m => m.Kind == MilestoneKind.TitleCompleted && m.Title == "RISE CHALLENGER");
        Assert.Contains(result.Milestones,
            m => m.Kind == MilestoneKind.ParagonLevelGain && m.Title == "Expert Lv. 2");
        Assert.DoesNotContain(result.Milestones, m => m.Kind == MilestoneKind.PumbilityGain);
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
